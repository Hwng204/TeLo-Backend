using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface ISchoolService
{
    /// <summary>Danh sách trường, có thể lọc theo từ khoá (tên / mã).</summary>
    Task<SchoolListPage> ListAsync(string? search, CancellationToken cancellationToken);

    /// <summary>Chi tiết một trường kèm danh sách cơ sở.</summary>
    Task<ServiceResult<SchoolDetailDto>> GetByIdAsync(ulong id, CancellationToken cancellationToken);

    /// <summary>Tạo trường mới.</summary>
    Task<ServiceResult<SchoolListItem>> CreateAsync(CreateSchoolRequest request, CancellationToken cancellationToken);

    /// <summary>Cập nhật thông tin trường (không đổi mã).</summary>
    Task<ServiceResult<SchoolListItem>> UpdateAsync(ulong id, UpdateSchoolRequest request, CancellationToken cancellationToken);
}
