namespace Quorum.Backend.Radzen.Sample.Models;


public enum ItemStatus
{
    Draft,
    Active,
    Pending,
    Archived
}

public class SampleDataItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ItemStatus Status { get; set; }
    public decimal Price { get; set; }
    public double Rating { get; set; }
    public int StockQuantity { get; set; }
    public bool IsInStock { get; set; }
    public DateTime CreatedAt { get; set; }
    public TimeOnly DeliveryTime { get; set; }
    public List<string> Tags { get; set; } = new();

    public static List<SampleDataItem> GenerateSampleData(int count = 100)
    {
        var random = new Random(42);
        var categories = new[] { "Electronics", "Hardware", "Software", "Services", "Books" };
        var availableTags = new[] { "Critical", "Bestseller", "Sale", "New", "Limited", "Refurbished" };

        var list = new List<SampleDataItem>();

        for (int i = 1; i <= count; i++)
        {
            var category = categories[random.Next(categories.Length)];
            var price = Math.Round((decimal)(random.NextDouble() * 500 + 10), 2);
            var inStock = random.NextDouble() > 0.2;

            list.Add(new SampleDataItem
            {
                Id = i,
                Code = $"QRM-{1000 + i}",
                Name = $"{category} Item #{i}",
                Category = category,
                Status = (ItemStatus)random.Next(0, 4),
                Price = price,
                Rating = Math.Round(random.NextDouble() * 4 + 1, 1),
                StockQuantity = inStock ? random.Next(1, 250) : 0,
                IsInStock = inStock,
                CreatedAt = DateTime.Now.AddDays(-random.Next(1, 1000)).AddHours(random.Next(0, 24)),
                DeliveryTime = new TimeOnly(random.Next(8, 18), random.Next(0, 60)),
                Tags = availableTags.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList()
            });
        }

        return list;
    }
}