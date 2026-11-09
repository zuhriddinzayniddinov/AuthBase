using Core.Domain.Models.Api;
using Entity.DataTransferObjects.Authentication;
using Entity.DataTransferObjects.Users;

namespace AuthService.Services;

public interface IUserService
{
    Task<ResponseModel<List<UserDto>>> GetUsersAsync(MetaQueryModel metaQueryModel, long userId);
    Task<ResponseModel<UserFullDto>> GetUserByIdAsync(long userId);
    Task<ResponseModel<bool>> AddStructureAsync(ChangeUserStructureDto userStructure);
    Task<ResponseModel<bool>> RemoveStructureAsync(ChangeUserStructureDto userStructure);
}
