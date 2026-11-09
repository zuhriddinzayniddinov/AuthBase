using System.IdentityModel.Tokens.Jwt;
using Core.Domain.Constants;
using Core.Domain.Enums;
using Core.Domain.Exceptions;
using Core.Domain.Helpers;
using Core.Infrastructure.Security;
using Core.Web.Constants;
using DatabaseBroker.DataContext;
using DatabaseBroker.Repositories.Auth;
using Entity.DataTransferObjects.Authentication;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AuthService.Services;

public class AuthService(
    IJwtTokenHandler jwtTokenHandler,
    IUserRepository userRepository,
    ITokenRepository tokenRepository,
    ISignMethodsRepository signMethodsRepository,
    IStructureRepository structureRepository,
    AuthDataContext authDataContext,
    IDistributedCache distributedCache)
    : IAuthService
{
    public async Task<bool> RegisterAsync(UserRegisterDto userRegisterDto)
    {
        var hasStoredUser = await signMethodsRepository.OfType<DefaultSignMethod>()
            .AnyAsync(x => x.Username == userRegisterDto.UserName);
        if (hasStoredUser)
            throw new AlreadyExistsException("User name or login already exists");

        var defaultStructure = await structureRepository.FirstOrDefaultAsync(x => x.Type == 1);

        var newUser = new User
        {
            FullName = userRegisterDto.FullName,
            Gender = userRegisterDto.Gender,
            Pinfl = userRegisterDto.Pinfl,
            BirthDate = userRegisterDto.BirthDate.ToDateTime(new TimeOnly(0, 0, 0)),
            AvatarUrl = userRegisterDto.PhotoUrl,
            SignMethods = new List<SignMethod>(),
            Structures = defaultStructure != null
                ? new List<UserStructure>
                {
                    new()
                    {
                        StructureId = defaultStructure.Id
                    }
                }
                : new List<UserStructure>()
        };

        await using var transaction = await authDataContext.Database.BeginTransactionAsync();
        try
        {
            var storedUser = await userRepository.AddAsync(newUser);
            var salt = Guid.NewGuid().ToString();
            await signMethodsRepository.AddAsync(new DefaultSignMethod
            {
                Username = userRegisterDto.UserName,
                Salt = salt,
                PasswordHash = PasswordHelper.Encrypt(userRegisterDto.Password, salt),
                UserId = storedUser.Id
            });

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TokenDto> SignByPasswordAsync(AuthenticationDto authenticationDto)
    {
        var signMethod = await signMethodsRepository.OfType<DefaultSignMethod>()
            .Include(x => x.User)
            .ThenInclude(u => u.Structures)
            .FirstOrDefaultAsync(x => x.Username == authenticationDto.UserName);

        if (signMethod is null)
            throw new NotFoundException("That credentials not found");

        if (!PasswordHelper.Verify(signMethod.PasswordHash, authenticationDto.Password, signMethod.Salt))
            throw new NotFoundException("User not found");

        var user = signMethod.User;

        var refreshToken = jwtTokenHandler.GenerateRefreshToken();
        var structureIds = user.Structures?.Select(s => s.StructureId);
        var accessToken = jwtTokenHandler.GenerateAccessToken(user.Id, structureIds);

        var token = new TokenModel
        {
            UserId = user.Id,
            TokenType = TokenTypes.Normal,
            AccessTokenId = accessToken.jti,
            ExpireToken = accessToken.expireDate,
            RefreshToken = refreshToken.refreshToken,
            ExpireRefreshToken = refreshToken.expireDate
        };

        token = await tokenRepository.AddAsync(token);

        var tokenDto = new TokenDto(
            new JwtSecurityTokenHandler().WriteToken(accessToken.token),
            EncryptionHelper.EncryptStringWithTime(token.RefreshToken, token.Id, StaticCache.SymmetricKey, token.ExpireRefreshToken),
            token.ExpireRefreshToken);

        return tokenDto;
    }

    public async Task<TokenDto> RefreshTokenAsync(TokenDto tokenDto)
    {
        var tokenModel = EncryptionHelper.DecryptStringWithTime(tokenDto.RefreshToken, StaticCache.SymmetricKey);

        if (tokenModel.Timestamp < DateTime.UtcNow)
            throw new AlreadyExistsException("Refresh token timed out");

        var token = await tokenRepository
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(t => t.Id == tokenModel.Id && t.RefreshToken == tokenModel.Token)
            ?? throw new NotFoundException("Not Found Token");

        if (token.ExpireRefreshToken < DateTime.UtcNow)
            throw new AlreadyExistsException("Refresh token timed out");

        token.User = await userRepository.GetAllAsQueryable()
            .Include(u => u.Structures)
            .FirstOrDefaultAsync(u => u.Id == token.UserId)
            ?? throw new NotFoundException("Not Found User");

        var structureIds = token.User.Structures?.Select(s => s.StructureId);
        var accessToken = jwtTokenHandler.GenerateAccessToken(token.User.Id, structureIds);
        var refreshToken = jwtTokenHandler.GenerateRefreshToken();

        token.AccessTokenId = accessToken.jti;
        token.ExpireToken = accessToken.expireDate;
        token.RefreshToken = refreshToken.refreshToken;
        token.ExpireRefreshToken = refreshToken.expireDate;

        token = await tokenRepository.UpdateAsync(token);

        var newTokenDto = new TokenDto(
            new JwtSecurityTokenHandler().WriteToken(accessToken.token),
            EncryptionHelper.EncryptStringWithTime(token.RefreshToken, token.Id, StaticCache.SymmetricKey, token.ExpireRefreshToken),
            token.ExpireRefreshToken);

        return newTokenDto;
    }

    public async Task<bool> DeleteTokenAsync(string jti)
    {
        var token = await tokenRepository
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(t => t.AccessTokenId == jti)
            ?? throw new NotFoundException("Not Found Token");

        var deleteToken = await tokenRepository.RemoveAsync(token);

        if (deleteToken != null && deleteToken.Id == token.Id)
        {
            var redisKey = string.Format(AppCacheKeys.SessionRevokedL2Format, token.UserId, token.AccessTokenId);
            var remainingTtl = token.ExpireToken > DateTime.UtcNow
                ? token.ExpireToken - DateTime.UtcNow
                : TimeSpan.FromMinutes(15);

            await distributedCache.SetStringAsync(redisKey, "revoked", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = remainingTtl
            });

            return true;
        }

        return false;
    }

    public Task<List<int>> GetUserPermissionsAsync(long userId)
    {
        return userRepository.GetAllAsQueryable()
            .Where(u => u.Id == userId)
            .SelectMany(u =>
                u.Structures
                    .SelectMany(s =>
                        s.Structure.StructurePermissions
                            .Where(sp => !sp.IsDelete && !sp.Permission.IsDelete)
                            .Select(sp => sp.Permission.Code)))
            .Distinct()
            .ToListAsync();
    }
}
