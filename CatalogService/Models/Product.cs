namespace CatalogService.Models
{

public record Product(int Id, string Name, decimal Price);
public record ProductCreateDto(string Name, decimal Price);

}