namespace CarShell.Web.Data.Entities;

public class VehicleModel
{
    public int Id { get; set; }
    public int MakeId { get; set; }
    public Make Make { get; set; } = default!;
    public string Name { get; set; } = default!;
}
