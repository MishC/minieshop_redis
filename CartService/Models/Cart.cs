namespace CartService.Models;

public record Cart(string UserId, List<CartItem> Items);