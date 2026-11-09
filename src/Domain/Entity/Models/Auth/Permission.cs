using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Entity.Models.Auth;

[Table("permissions", Schema = "auth"), Index(nameof(Code), IsUnique = true)]
public class Permission : ModelBase<long>
{
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("code")] public int Code { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("module_name")] public string? ModuleName { get; set; }
    [Column("service_name")] public string? ServiceName { get; set; }
}
