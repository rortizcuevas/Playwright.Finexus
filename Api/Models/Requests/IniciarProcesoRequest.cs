namespace Playwright.Finexus.Api;

public class IniciarProcesoRequest
{
	public string TipoIdentificacion { get; set; } = string.Empty;
	public string Identificacion { get; set; } = string.Empty;
	public string Nombre { get; set; } = string.Empty;
	public string SegundoNombre { get; set; } = string.Empty;
	public string Apellido { get; set; } = string.Empty;
	public string SegundoApellido { get; set; } = string.Empty;
	public string CorreoElectronico { get; set; } = string.Empty;
	public string Telefono { get; set; } = string.Empty;
}
