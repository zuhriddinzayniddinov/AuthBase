using Core.Domain.Exceptions;
using Core.Domain.Models.Api;
using Core.Infrastructure.Data.Extensions;
using DatabaseBroker.Repositories.Auth;
using Entity.DataTransferObjects.Role;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace RoleService.Service;

public class RoleService(
    IStructureRepository structureRepository,
    IPermissionRepository permissionsRepository,
    IStructurePermissionRepository structurePermissionsRepository)
    : IRoleService
{
    public async Task<ResponseModel<StructureDto>> CreateStructureAsync(StructureForCreationDto structureForCreationDto)
    {
        var newStructure = new Structure
        {
            Name = structureForCreationDto.Name,
            StructurePermissions = structureForCreationDto.PermissionIds.Select(p => new StructurePermission
            {
                PermissionId = p
            }).ToList()
        };

        var structureModel = await structureRepository.AddAsync(newStructure);

        return new StructureDto(
            structureModel.Id,
            structureModel.Name);
    }

    public async Task<ResponseModel<StructureDto>> ModifyStructureAsync(StructureForModificationDto structure)
    {
        var newStructure = await structureRepository.GetAllAsQueryable()
            .Where(s => s.Id == structure.Id)
            .Include(s => s.StructurePermissions.Where(sp => !sp.Permission.IsDelete))
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("Not found structure");

        newStructure.Name = structure.Name;

        newStructure.StructurePermissions = newStructure.StructurePermissions.Select(sp =>
        {
            sp.IsDelete = !structure.PermissionIds.Contains(sp.PermissionId);
            return sp;
        }).ToList();

        var existingPermissionIds = newStructure.StructurePermissions.Select(sp => sp.PermissionId).ToHashSet();
        var toAdd = structure.PermissionIds
            .Where(p => !existingPermissionIds.Contains(p))
            .Select(p => new StructurePermission
            {
                PermissionId = p,
                StructureId = newStructure.Id
            })
            .ToArray();

        if (toAdd.Length > 0)
        {
            await structurePermissionsRepository.AddRangeAsync(toAdd);
        }

        var changedStructure = await structureRepository.UpdateAsync(newStructure);

        return new StructureDto(
            changedStructure.Id,
            changedStructure.Name);
    }

    public async Task<ResponseModel<bool>> RemoveStructureAsync(long structureId)
    {
        var deletedStructure = await structureRepository.RemoveAsync(new Structure
        {
            Id = structureId
        }) ?? throw new NotFoundException("Not found structure");

        return true;
    }

    public async Task<ResponseModel<List<StructureDto>>> RetrieveStructureAsync(MetaQueryModel metaQueryModel)
    {
        var query = structureRepository
            .GetAllAsQueryable()
            .FilterByExpressions(metaQueryModel);

        var total = await query.CountAsync();
        var items = await query
            .Sort(metaQueryModel)
            .Paging(metaQueryModel)
            .Select(s => new StructureDto(s.Id, s.Name))
            .ToListAsync();

        return ResponseModel<List<StructureDto>>.ResultFromContent(items, total: total);
    }

    public async Task<ResponseModel<PermissionDto>> ModifyPermissionAsync(PermissionDto permissionDto)
    {
        var newPermission = await permissionsRepository.GetByIdAsync(permissionDto.Id)
            ?? throw new NotFoundException("Not found permission");

        newPermission.Name = permissionDto.Name;

        await permissionsRepository.UpdateAsync(newPermission);

        return permissionDto;
    }

    public async Task<ResponseModel<List<PermissionDto>>> RetrievePermissionAsync(MetaQueryModel metaQueryModel)
    {
        var query = permissionsRepository.GetAllAsQueryable()
            .FilterByExpressions(metaQueryModel);

        var total = await query.CountAsync();
        var items = await query
            .Select(p => new PermissionDto(p.Id, p.Code, p.Name))
            .Paging(metaQueryModel)
            .ToListAsync();

        return ResponseModel<List<PermissionDto>>.ResultFromContent(items, total: total);
    }

    public async Task<ResponseModel<List<PermissionDto>>> RetrieveStructurePermissionByStructureIdAsync(MetaQueryModel metaQueryModel)
    {
        var query = structurePermissionsRepository.GetAllAsQueryable()
            .FilterByExpressions(metaQueryModel);

        var total = await query.CountAsync();
        var items = await query
            .Sort(metaQueryModel)
            .Paging(metaQueryModel)
            .Select(sp => new PermissionDto(sp.PermissionId, sp.Permission.Code, sp.Permission.Name))
            .ToListAsync();

        return ResponseModel<List<PermissionDto>>.ResultFromContent(items, total: total);
    }
}
