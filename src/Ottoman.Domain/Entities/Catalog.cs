namespace Ottoman.Domain.Entities;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string? ImageKey { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EmployeeService> Employees { get; set; } = [];
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public List<ProductImage> Images { get; set; } = [];
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string FileKey { get; set; } = null!;
    public int SortOrder { get; set; }
}
