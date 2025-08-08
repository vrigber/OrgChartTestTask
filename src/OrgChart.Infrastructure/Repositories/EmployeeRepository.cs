using Microsoft.EntityFrameworkCore;
using OrgChart.Core.Interfaces;
using OrgChart.Core.Models;
using OrgChart.Infrastructure.Entities;

namespace OrgChart.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly OrgChartDbContext _context;
    public EmployeeRepository(OrgChartDbContext context)
    {
        _context = context;
    }

    public Task<Employee?> GetByIdOrDefault(int id)
    {
        return _context.Employees
            .Include(e => e.Manager)
            .Include(e => e.Subordinates)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public Task<List<Employee>> GetAll()
    {
        return _context.Employees
            .Include(e => e.Manager)
            .Include(e => e.Subordinates)
            .AsSplitQuery().ToListAsync();
    }

    public async Task<Employee> AddEmployee(Employee employee)
    {
        await _context.Employees.AddAsync(employee);
        await _context.SaveChangesAsync();
        return employee;
    }

    public Task UpdateEmployee(Employee employee)
    {
        _context.Employees.Update(employee);
        return _context.SaveChangesAsync();
    }

    public Task DeleteEmployee(Employee employee)
    {
        _context.Employees.Remove(employee);
        return _context.SaveChangesAsync();
    }

    public Task<int> GetSubordinateCount(int employeeId)
    {
        return _context.Employees.CountAsync(e => e.ManagerId == employeeId);
    }

    public Task<int> GetHierarchyDepth(int employeeId)
    {
        return _context.Set<HierarchyDepthResult>().FromSqlInterpolated($@"WITH RECURSIVE cte AS (
  SELECT id, manager_id, 1 AS depth
  FROM orgchart.employees
  WHERE id = {employeeId}

  UNION ALL

  SELECT i.id, i.manager_id, cte.depth + 1
  FROM orgchart.employees i
  JOIN cte ON i.id = cte.manager_id
  WHERE cte.depth < 7      
)
SELECT MAX(depth) AS ""Depth""
FROM cte").Select(x => x.Depth).SingleAsync();
    }

    public Task<bool> HasCycle(int employeeId, int newManagerId)
    {
        return _context.Employees.FromSqlInterpolated($@"WITH RECURSIVE cte AS (
  SELECT id, manager_id, 1 AS depth
  FROM orgchart.employees
  WHERE id = {employeeId}

  UNION ALL

  SELECT i.id, i.manager_id, cte.depth + 1
  FROM orgchart.employees i
  JOIN cte ON i.manager_id = cte.id
  WHERE cte.depth < 7      
)
SELECT *
FROM cte").AnyAsync(e => e.Id == newManagerId);
    }
}