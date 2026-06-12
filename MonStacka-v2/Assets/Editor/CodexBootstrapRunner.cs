using UnityEditor;
using MonStacka.Editor;
public static class CodexBootstrapRunner {
  public static void Run() {
    MonStackaV2Bootstrap.Run();
    EditorApplication.Exit(0);
  }
}
