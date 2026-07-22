using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeCore.Data;
using VibeCore.Models;
using VibeCore.Security;
using Microsoft.AspNetCore.Authorization;

namespace VibeCore.Areas.Api.Controllers;

[Area("Api")]
[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AppPolicies.Reader)]
public class TodosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TodosController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Todos
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodos()
    {
        return await _context.Todos
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // GET: api/Todos/5
    [HttpGet("{id}")]
    public async Task<ActionResult<TodoItem>> GetTodoItem(int id)
    {
        var todoItem = await _context.Todos.FindAsync(id);

        if (todoItem == null)
        {
            return NotFound();
        }

        return todoItem;
    }

    // POST: api/Todos
    [HttpPost]
    public async Task<ActionResult<TodoItem>> PostTodoItem(TodoItem todoItem)
    {
        todoItem.CreatedAt = DateTime.UtcNow;
        _context.Todos.Add(todoItem);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTodoItem), new { id = todoItem.Id }, todoItem);
    }

    // PUT: api/Todos/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutTodoItem(int id, TodoItem todoItem)
    {
        if (id != todoItem.Id)
        {
            return BadRequest();
        }

        _context.Entry(todoItem).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TodoItemExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // DELETE: api/Todos/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodoItem(int id)
    {
        var todoItem = await _context.Todos.FindAsync(id);
        if (todoItem == null)
        {
            return NotFound();
        }

        _context.Todos.Remove(todoItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PATCH: api/Todos/5/complete
    [HttpPatch("{id}/complete")]
    public async Task<IActionResult> CompleteTodoItem(int id)
    {
        var todoItem = await _context.Todos.FindAsync(id);
        if (todoItem == null)
        {
            return NotFound();
        }

        todoItem.IsCompleted = true;
        todoItem.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool TodoItemExists(int id)
    {
        return _context.Todos.Any(e => e.Id == id);
    }
}
