using UnityEngine;
using System.Collections;
using WebSocketSharp;
using TMPro;
using System.Text;
using Unity.WebRTC;

public class SignalingClient : MonoBehaviour
{
    public TextMeshProUGUI SignalStatus;
    public TextMeshProUGUI LogText;

    private WebSocket ws;
    private StringBuilder stringBuilder;

    private RTCPeerConnection localConnection;
    private RTCPeerConnection remoteConnection;
    private RTCDataChannel sendChannel;
    private RTCDataChannel receiveChannel;

    public void ConnectToSignalServer()
    {
        ws.Connect();
    }
    public void DisconnectToSignalServer()
    {
        ws.Close();
    }

    public void PingSignalServer()
    {
        ws.Send("Unity-Ping");
    }

    private void Log(string line)
    {
        if (stringBuilder == null)
        {
            stringBuilder = new StringBuilder();
        }

        Debug.Log(line);
        stringBuilder.AppendLine(line);
        LogText.text = stringBuilder.ToString();
    }

    void Awake()
    {
        // Replace with your signaling server URL
        string url = "ws://127.0.0.1:8080";

        ws = new WebSocket(url);

        ws.OnOpen += OnWebSocketOpen;
        ws.OnMessage += OnWebSocketMessage;
        ws.OnError += OnWebSocketError;
        ws.OnClose += OnWebSocketClose;

    }

    private void OnWebSocketOpen(object sender, System.EventArgs e)
    {
        Debug.Log("Connected to the signaling server.");

        SignalStatus.text = "Signaling Server: Connected";

        StartCoroutine(SetupWebRTC());
    }

    private void OnWebSocketMessage(object sender, MessageEventArgs e)
    {
        // Handle incoming data here
        Debug.Log("Message from server: " + e.Data);
    }

    private void OnWebSocketError(object sender, ErrorEventArgs e)
    {
        Debug.LogError("WebSocket Error: " + e.Message);
    }

    private void OnWebSocketClose(object sender, CloseEventArgs e)
    {
        Debug.Log("WebSocket closed with reason: " + e.Reason);
        SignalStatus.text = "Signaling Server: Disconnected";
    }

    void OnDestroy()
    {
        if (ws != null) ws.Close();
        if (sendChannel != null) sendChannel.Close();
        if (receiveChannel != null) receiveChannel.Close();
        if (localConnection != null) localConnection.Close();
        if (remoteConnection != null) remoteConnection.Close();
    }



    IEnumerator SetupWebRTC()
    {
        // Create local peer
        localConnection = new RTCPeerConnection();
        sendChannel = localConnection.CreateDataChannel("sendChannel");
        sendChannel.OnOpen += HandleSendChannelStatusChange;
        sendChannel.OnClose += HandleSendChannelStatusChange;

        // Create remote peer
        remoteConnection = new RTCPeerConnection();
        remoteConnection.OnDataChannel = ReceiveChannelCallback;

        // Register ICE candidates
        localConnection.OnIceCandidate = e => {
            if (!string.IsNullOrEmpty(e.Candidate))
                remoteConnection.AddIceCandidate(e);
        };

        remoteConnection.OnIceCandidate = e => {
            if (!string.IsNullOrEmpty(e.Candidate))
                localConnection.AddIceCandidate(e);
        };

        // Begin the signaling process
        var op1 = localConnection.CreateOffer();
        yield return op1;

        var offerDesc = op1.Desc;
        yield return localConnection.SetLocalDescription(ref offerDesc);
        yield return remoteConnection.SetRemoteDescription(ref offerDesc);

        var op4 = remoteConnection.CreateAnswer();
        yield return op4;

        var answerDesc = op4.Desc;
        yield return remoteConnection.SetLocalDescription(ref answerDesc);
        yield return localConnection.SetRemoteDescription(ref answerDesc);

        // Monitor ICE connection status
        localConnection.OnIceConnectionChange = state => {
            Debug.Log($"Local ICE Connection State: {state}");
        };

        remoteConnection.OnIceConnectionChange = state => {
            Debug.Log($"Remote ICE Connection State: {state}");
        };
    }

    private void HandleSendChannelStatusChange()
    {
        if (sendChannel.ReadyState == RTCDataChannelState.Open)
        {
            Debug.Log("Send channel open.");
            // Now ready to send messages
        }
        else if (sendChannel.ReadyState == RTCDataChannelState.Closed)
        {
            Debug.Log("Send channel closed.");
        }
    }

    private void ReceiveChannelCallback(RTCDataChannel channel)
    {
        Debug.Log("Received remote data channel.");
        receiveChannel = channel;
        receiveChannel.OnMessage = HandleReceiveMessage;
    }

    public void SendChannelMessage(string message)
    {
        if (sendChannel != null && sendChannel.ReadyState == RTCDataChannelState.Open)
        {
            sendChannel.Send(message);
        }
    }

    public void SendBinary(byte[] bytes)
    {
        if (sendChannel != null && sendChannel.ReadyState == RTCDataChannelState.Open)
        {
            sendChannel.Send(bytes);
        }
    }

    private void HandleReceiveMessage(byte[] bytes)
    {
        var message = Encoding.UTF8.GetString(bytes);
        Debug.Log($"Received: {message}");
    }
}