using UnityEngine;
using UnityEditor;
using System.Net;
using System.Text;
using System.Threading;

[InitializeOnLoad]
public static class McpBridge
{
    private static HttpListener listener;
    private static Thread thread;

    static McpBridge()
    {
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();
            thread = new Thread(() =>
            {
                while (listener != null && listener.IsListening)
                {
                    var ctx = listener.GetContext();
                    byte[] b = Encoding.UTF8.GetBytes("{\"status\":\"connected\"}");
                    ctx.Response.OutputStream.Write(b, 0, b.Length);
                    ctx.Response.Close();
                }
            });
            thread.IsBackground = true;
            thread.Start();
            Debug.Log("[MCP] Unity bağlandı, Antigravity bekleniyor!");
        }
        catch { }
    }
}