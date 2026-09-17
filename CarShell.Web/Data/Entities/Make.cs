namespace CarShell.Web.Data.Entities;

public class Make
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
}
