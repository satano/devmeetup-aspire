using Microsoft.EntityFrameworkCore;
using Todo.Api.Models;

namespace Todo.Api.Data;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
	public DbSet<TodoItem> Todos => Set<TodoItem>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<TodoItem>(entity =>
		{
			entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
		});
	}
}
