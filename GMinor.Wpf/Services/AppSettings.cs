namespace GMinor.Wpf.Services;

public record FileRouterSettings(string SourceFolder, string DestinationFolder);
public record AppSettings(FileRouterSettings FileRouter);
