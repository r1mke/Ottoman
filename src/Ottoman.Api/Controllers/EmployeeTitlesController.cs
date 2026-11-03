using Microsoft.AspNetCore.Authorization;
using Ottoman.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Employees;

namespace Ottoman.Api.Controllers;

/// <summary>The list of titles offered when a barber is created or edited.</summary>
[ApiController]
[Route("api/v1/employee-titles")]
[Authorize(Policy = Policies.AdminOnly)]
public class EmployeeTitlesController(IEmployeeTitles titles) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EmployeeTitleDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = PageQuery.MaxPageSize, CancellationToken ct = default) =>
        this.Page(await titles.ListAsync(new PageQuery(page, pageSize), ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeTitleDto>> Add(AddEmployeeTitleRequest request, CancellationToken ct)
    {
        var result = await titles.AddAsync(request.Name, ct);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeTitleDto>> Update(int id, UpdateEmployeeTitleRequest request, CancellationToken ct) =>
        this.ToActionResult(await titles.UpdateAsync(id, request.Name, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await titles.DeleteAsync(id, ct));
}
