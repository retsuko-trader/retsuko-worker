using OpenTelemetry.Trace;

public static class MyTracer {
  const string SERVICE_NAME = "retsuko-worker";

  public static readonly Tracer Tracer = TracerProvider.Default.GetTracer(SERVICE_NAME);
}
