using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(DataPlatformDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await db.Users
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.PreferredOrigin,
                u.PreferredAirline,
                u.MaxBudget
            })
            .ToListAsync();

        return Ok(users);
    }
}