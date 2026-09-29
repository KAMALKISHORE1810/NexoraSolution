using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoraAPI.Models;
using NexoraAPI.Dtos;
using NexoraAPI.Data;
namespace ApexBankApi.Controllers;

[Route("api/[controller]")]

[ApiController]
public class AuthController(ApplicationDbContext context) : ControllerBase
{

    private readonly ApplicationDbContext _context = context;


    // GET: api/User
    [HttpPost("getuser")]
    public async Task<ActionResult<ApplicationUser>> GetUser([FromBody] UserDto u)
    { 
        if (u == null)
            return BadRequest();
        Console.WriteLine(u.Username + " " + u.Password);
        var user = await _context.ApplicationUsers
            .FirstOrDefaultAsync(x =>
                x.Username == u.Username &&
                x.IsActive
            );
        Console.WriteLine("user.Username" + user?.Username);
        if (user == null)
            return Unauthorized();

        return Ok(user);
    }
}