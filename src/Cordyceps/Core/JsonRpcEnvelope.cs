using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cordyceps.Core
{
    /// <summary>
    /// Pure, host-free JSON-RPC 2.0 response-envelope policy for the MCP HTTP transport
    /// (unit-tested in Cordyceps.Tests). Extracted from <c>McpServer.HandleMcpPostAsync</c> so the
    /// id-echo contract can't silently regress:
    ///
    /// <para>- Notifications are detected from an ABSENT <c>id</c> property only. An explicit
    /// <c>"id": null</c> is NOT a notification — it still gets a response, with <c>"id": null</c>
    /// echoed back.</para>
    ///
    /// <para>- The id is echoed losslessly via <see cref="JToken.DeepClone"/>: long, fractional
    /// (1.0 stays 1.0, not 1), string, and null ids round-trip exactly as the client sent them.</para>
    ///
    /// <para>- An explicit JSON null id is a <see cref="JValue"/> whose <see cref="JToken.Type"/> is
    /// <see cref="JTokenType.Null"/> — a NON-null reference. So <c>NullValueHandling.Ignore</c> in
    /// <see cref="Serialize"/> serializes it as <c>null</c> rather than stripping the <c>"id"</c>
    /// property, while a genuinely absent member (null reference) is still dropped. This mirrors
    /// the WhenWritingNull behavior the transport had under System.Text.Json.</para>
    ///
    /// <para>Callers compute <see cref="EchoId"/> BEFORE dispatching the method: echoing up front
    /// means a malformed id can never destroy the response after the tool's side effects ran
    /// (which would make the client retry and double-apply).</para>
    ///
    /// <para>Newtonsoft rather than System.Text.Json: see the note in <see cref="JsonTypeConverter"/>
    /// — STJ's net48 build needs newer System.Memory/Unsafe than Rhino 7 already has loaded.</para>
    /// </summary>
    public static class JsonRpcEnvelope
    {
        // Matches the serialization the transport has always used: compact, and Ignore so a
        // genuinely-null "result" member is dropped while a JTokenType.Null id survives.
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// JSON-RPC 2.0: a request is a notification (MUST NOT receive a response) only when the
        /// <c>id</c> property is absent. An explicit <c>"id": null</c> is not a notification.
        /// </summary>
        public static bool IsNotification(JToken request)
        {
            var obj = request as JObject;
            if (obj == null)
                return true;
            return !obj.TryGetValue("id", out _);
        }

        /// <summary>
        /// The id to echo in the response: a lossless <see cref="JToken.DeepClone"/> of the request
        /// id (safe to use after the source document goes out of scope), or null when the request is
        /// a notification (no response is sent at all).
        /// </summary>
        public static JToken EchoId(JToken request)
        {
            if (IsNotification(request))
                return null;
            return ((JObject)request)["id"].DeepClone();
        }

        /// <summary>
        /// Build the JSON-RPC 2.0 response envelope. Emits <c>error</c> (with
        /// <paramref name="errorCode"/>/<paramref name="errorMessage"/>) when
        /// <paramref name="errorMessage"/> is non-null, otherwise <c>result</c>. The
        /// <c>id</c> member is included exactly when <paramref name="responseId"/> is non-null —
        /// including an explicit <c>"id": null</c> when the client sent null (that arrives as a
        /// JTokenType.Null token, not a null reference).
        /// </summary>
        public static Dictionary<string, object> Build(JToken responseId, object result, string errorMessage, int errorCode)
        {
            var responseObj = new Dictionary<string, object> { ["jsonrpc"] = "2.0" };

            if (responseId != null)
                responseObj["id"] = responseId;

            if (errorMessage != null)
            {
                responseObj["error"] = new Dictionary<string, object>
                {
                    ["code"] = errorCode,
                    ["message"] = errorMessage
                };
            }
            else
            {
                responseObj["result"] = result;
            }

            return responseObj;
        }

        /// <summary>
        /// Serialize a response envelope with the transport's serializer settings (compact,
        /// NullValueHandling.Ignore — see the class remarks for why a null id still survives).
        /// </summary>
        public static string Serialize(Dictionary<string, object> response)
        {
            return JsonConvert.SerializeObject(response, SerializerSettings);
        }
    }
}
