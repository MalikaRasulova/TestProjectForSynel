using System.ComponentModel.DataAnnotations;

namespace SynelTestProject.Models;

public sealed class EmployeeUpdateRequest
{
    [Required]
    public required Dictionary<string, string?> Values { get; init; }
}
