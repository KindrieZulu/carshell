using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarShell.Web.Pages;

public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }
    public int HttpStatusCode { get; private set; }
    public string Heading { get; private set; } = "Something went wrong";
    public string Message { get; private set; } = "An unexpected error occurred. Try again, or head back to the homepage.";

    public void OnGet(int? statusCode)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        HttpStatusCode = statusCode ?? 500;

        (Heading, Message) = HttpStatusCode switch
        {
            404 => ("Page not found", "That page, or listing, does not exist or may have been removed."),
            403 => ("Access denied", "You do not have permission to view this page."),
            _ => (Heading, Message),
        };
    }
}
