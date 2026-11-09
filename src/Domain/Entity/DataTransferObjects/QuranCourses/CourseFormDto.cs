using Entity.Enums;
using Entity.Models.QuranCourses;

namespace Entity.DataTransferObjects.QuranCourses;

public record CourseFormDto(
    long Id,
    string MarkerId,
    string Name,
    string Description,
    int AdmissionQuota,
    int Duration,
    Gender? ForGender,
    WorkingHour TransitionTime,
    long TrainingCenterId) : BaseDto<long>(Id);