/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System;
using Byn.Net;
using System.Collections.Generic;
using Byn.Common;

/// <summary>
/// Contains a complete chat example.
/// It can run on Windows x86/x64 and in browsers. More platforms will be added soon.
/// 
/// The chat app will report during start which system it uses.
/// 
/// The user can enter a room name and click the "Open room" button to start a server and wait for
/// incoming connections or use the "Join room" button to join an already existing room.
/// 
/// 
/// 
/// 
/// As the system implements a server/client style connection all messages will first be sent to the
/// server and the server delivers it to each client. The server side ConnectionId is used to
/// identify a user.
/// 
/// 
/// </summary>
public class RealSocket
{
    /// <summary>
    /// This is a test server. Don't use in production! The server code is in a zip file in WebRtcNetwork
    /// </summary>
	public string websocketUrl = "ws://10.0.2.11:18080/";

    //public string uIceServer = "stun:because-why-not.com:12779";
	public string uIceServer = "stun:stun.l.google.com:19302";
    public string uIceServerUser = "";
    public string uIceServerPassword = "";

    /// <summary>
    /// Mozilla stun server. Used to get trough the firewall and establish direct connections.
    /// Replace this with your own production server as well. 
    /// </summary>
  	public string uIceServer2 = "stun:stun.services.mozilla.com:3478";
  	//public string uIceServer2 = "stun:localhost:19302";

    /// <summary>
    /// The network interface.
    /// This can be native webrtc or the browser webrtc version.
    /// (Can also be the old or new unity network but this isn't part of this package)
    /// </summary>
    private IBasicNetwork mNetwork = null;

	private string nodeID = "";
	private List<string> neighborAddresses = new List<string>();
    private List<ConnectionId> mConnections = new List<ConnectionId>();
	private WebSocket ws;


	public RealSocket() {
		this.ws = new WebSocket(new Uri(this.websocketUrl));
    }

    private void Setup() {
		Debug.Log("Initializing webrtc network");

		mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(websocketUrl, new IceServer[] { new IceServer(uIceServer, uIceServerUser, uIceServerPassword), new IceServer(uIceServer2) });
        if (mNetwork == null)
			Debug.Log("Failed to access webrtc ");
        else
			Debug.Log("WebRTCNetwork created");
    }

    private void Reset() {
        mConnections = new List<ConnectionId>();
        this.mNetwork.Dispose();
        this.mNetwork = null;
    }


    void OnDestroy() {
		if (mNetwork != null) this.Reset();
    }

	/*
	 * 必ず一定の感覚で呼ばれる．
	 */
    private void FixedUpdate() {
        this.ReceiveNetwork();
    }
	private void ReceiveNetwork()
    {
        //check if the network was created
		if (mNetwork == null) { return; }

        //first update it to read the data from the underlaying network system
        mNetwork.Update();

        //handle all new events that happened since the last update
        NetworkEvent evt;
        //check for new messages and keep checking if mNetwork is available. it might get destroyed
        //due to an event
        while (mNetwork != null && mNetwork.Dequeue(out evt))
        {
            //print to the console for debugging
            Debug.Log(evt);

            //check every message
            switch (evt.Type) {
                case NetEventType.ServerInitialized:
                    string address = evt.Info; //addressは仮想アドレス（ルーム名）
					Debug.Log("Server started. Address: " + address);
                    break;
                case NetEventType.ServerInitFailed:
					Debug.Log("Server start failed.");
                    Reset();
                    break;
                case NetEventType.ServerClosed:
                    //server shut down. reaction to "Shutdown" call or
                    //StopServer or the connection broke down
					Debug.Log("Server closed. No incoming connections possible until restart.");
                    break;
                case NetEventType.NewConnection:
                    mConnections.Add(evt.ConnectionId);
                    //either user runs a client and connected to a server or the
                    //user runs the server and a new client connected
					Debug.Log("New local connection! ID: " + evt.ConnectionId);

                    //user runs a server. announce to everyone the new connection
                    //using the server side connection id as identification
                    string msg = "New user " + evt.ConnectionId + " joined the room.";
					Debug.Log(msg);
					this.BroadcastToNeighbors(msg);
                    break;
                case NetEventType.ConnectionFailed:
                    //Outgoing connection failed. Inform the user.
					Debug.Log("Connection failed");
                    Reset();
                    break;
                case NetEventType.Disconnected:
                    mConnections.Remove(evt.ConnectionId);
                    //A connection was disconnected
                    //If this was the client then he was disconnected from the server
                    //if it was the server this just means that one of the clients left
					Debug.Log("Local Connection ID " + evt.ConnectionId + " disconnected");
                    string userLeftMsg = "User " + evt.ConnectionId + " left the room.";

                    //show the server the message
					Debug.Log(userLeftMsg);

                    //other users left? inform them 
                    if (mConnections.Count > 0) {
						BroadcastToNeighbors(userLeftMsg);
                    }
                    break;
                case NetEventType.ReliableMessageReceived:
                case NetEventType.UnreliableMessageReceived:
                    ReceiveIncommingMessage(ref evt);
                    break;
				default:
					break;
            }
        }

        //finish this update by flushing the messages out if the network wasn't destroyed during update
        if(mNetwork != null)
            mNetwork.Flush();
    }

	/*
	 * メッセージを受信する．
	 */
	private void ReceiveIncommingMessage(ref NetworkEvent evt) {
        MessageDataBuffer buffer = (MessageDataBuffer)evt.MessageData;
        string msg = Encoding.UTF8.GetString(buffer.Buffer, 0, buffer.ContentLength);

		if (msg.IndexOf("SETTING_NODE@") == 0) {
			string newAddress = msg.Remove (0, "SETTING_NODE@".Length).Trim();
			this.mConnections.Add(evt.ConnectionId);
			ConnectionId cid = mNetwork.Connect(newAddress);
			Debug.Assert (cid == evt.ConnectionId);
		}
        //return the buffer so the network can reuse it
        buffer.Dispose();
    }

	/*
	 * 他のノードに指定したメッセージをブロードキャストする．
	 */
	public void BroadcastToNeighbors(string msg, bool reliable = true) {
		if (mNetwork == null || mConnections.Count == 0) {
			Debug.Log ("No connection. Can't send message.");
		}
		else {
			byte[] msgData = Encoding.UTF8.GetBytes (msg);
			foreach (ConnectionId id in mConnections) {
				mNetwork.SendData (id, msgData, 0, msgData.Length, reliable);
			}
		}
	}

	public IEnumerator Connect() {
		yield return this.ws.Connect ();
	}

	public IEnumerator Listen() {
		this.ws.SendString ("J");
		while (true) {
			string msg = this.ws.RecvString ();
			if (msg != null) {
				string[] nodeStrs = msg.Split (',');
				neighborAddresses = new List<string>(nodeStrs);
				int lastIndex = neighborAddresses.Count - 1;
				this.nodeID = neighborAddresses[lastIndex];
				neighborAddresses.RemoveAt(lastIndex);
				break;
			}
			if (this.ws.error != null) {
				Debug.LogError ("Error: " + this.ws.error);
				break;
			}
			yield return 0;
		}
		this.ConnectToWebRTC();
	}

	private void ConnectToWebRTC() {
		//WebRtcNetworkFactory factory = WebRtcNetworkFactory.Instance;
		if (mNetwork == null) this.Setup();

		mNetwork.StartServer(nodeID.ToString());
		Debug.Log("StartServer " + nodeID.ToString());

		foreach(string address in this.neighborAddresses) {
			ConnectionId cid = mNetwork.Connect(address);
			mConnections.Add(cid);
			Debug.Log("Connecting to " + address + " ...");
				
		}
	}

	public void Close() {
		string leaveSignal = "L" + this.nodeID;
		Debug.Log(leaveSignal);
		this.ws.SendString(leaveSignal);
		this.ws.Close();
	}
}
