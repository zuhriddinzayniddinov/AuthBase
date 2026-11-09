using Core.Domain.Exceptions;
using Core.Domain.Models.Api;
using Core.Infrastructure.Data.Extensions;
using Core.Infrastructure.Data.Repositories;
using Core.Web.Extensions;
using DatabaseBroker.Repositories.Auth;
using Entity.DataTransferObjects.Authentication;
using Entity.DataTransferObjects.Users;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using MinIoBroker.Services;

namespace AuthService.Services;

public class UserService(
    IUserRepository userRepository,
    GenericRepository<UserStructure, long> userStructureRepository,
    IMinioService minioService) : IUserService
{
    public async Task<ResponseModel<List<UserDto>>> GetUsersAsync(MetaQueryModel metaQueryModel, long userId)
    {
        var query = userRepository
            .GetAllAsQueryable()
            .FilterByExpressions(metaQueryModel);

        var total = await query.CountAsync();

        var items = await query
            .Sort(metaQueryModel)
            .Paging(metaQueryModel)
            .Select(u => new UserDto(
                u.Id,
                u.Id.EncryptId("userid", userId),
                u.FullName,
                u.AvatarUrl,
                u.Structures.Select(s => s.StructureId).ToList()))
            .ToListAsync();

        var mapped = await Task.WhenAll(items.Select(async u =>
        {
            if (string.IsNullOrWhiteSpace(u.AvatarUrl))
                return u;

            try
            {
                var presigned = await minioService.GetPresignedUrlAsync(u.AvatarUrl);
                return u with { AvatarUrl = presigned };
            }
            catch
            {
                return u;
            }
        }));

        return ResponseModel<List<UserDto>>.ResultFromContent(mapped.ToList(), total: total);
    }

    public async Task<ResponseModel<UserFullDto>> GetUserByIdAsync(long userId)
    {
        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Not found user");

        var avatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
            ? user.AvatarUrl
            : await minioService.GetPresignedUrlAsync(user.AvatarUrl);

        return ResponseModel<UserFullDto>.ResultFromContent(
            new UserFullDto(
                user.Id,
                user.FullName,
                avatarUrl,
                user.Structures.Select(s => s.StructureId).ToList(),
                user.Structures.Select(s => s.Structure.Name).ToList()));
    }

    public async Task<ResponseModel<bool>> AddStructureAsync(ChangeUserStructureDto userStructure)
    {
        if (await userStructureRepository.GetAllAsQueryable()
            .Where(us => us.UserId == userStructure.UserId)
            .Where(us => us.StructureId == userStructure.StructureId)
            .AnyAsync())
            throw new AlreadyExistsException("Structure already exists");

        await userStructureRepository.AddWithSaveChangesAsync(new UserStructure
        {
            UserId = userStructure.UserId,
            StructureId = userStructure.StructureId,
        });

        return true;
    }

    public async Task<ResponseModel<bool>> RemoveStructureAsync(ChangeUserStructureDto userStructure)
    {
        var userStructureModel = await userStructureRepository.GetAllAsQueryable()
            .Where(us => us.UserId == userStructure.UserId)
            .Where(us => us.StructureId == userStructure.StructureId)
            .FirstOrDefaultAsync() ?? throw new NotFoundException("Not found structure");

        await userStructureRepository.RemoveWithSaveChangesAsync(userStructureModel.Id);

        return true;
    }
}
