using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class SockjsClient : MonoBehaviour {
	
	public enum ConnectionState
	{
		Disconnected,
		Connecting,
		Connected
	}
	
	public delegate void OnMessageCallback(string _msg);
	public delegate void OnConnectCallback();
	public delegate void OnDisconnectCallback(int _code,string _message);
	
	public event OnMessageCallback OnMessage;
	public event OnDisconnectCallback OnDisconnect;
	public event OnConnectCallback OnConnect;
	
	private Hashtable m_sendHeader = new Hashtable();
	private ConnectionState m_state;
	private string m_xhr;
	private WWW m_wwwSending;
	private float m_sentTime;
	private WWW m_wwwPolling;
	private int m_ping;
	private List<string> m_outQueue = new List<string>();

	public ConnectionState State
	{
		get { return m_state; }
	}
	
	public bool Connected
	{
		get { return m_state == ConnectionState.Connected; }
	}

	public int Ping
	{
		get { return m_ping; }
	}

	// Use this for initialization
	public void Start () {
		
		m_sendHeader["Content-Type"] = "application/xml";
	}

	public void Update()
	{
		if (m_wwwSending != null && m_wwwSending.isDone)
		{
			if (m_wwwSending.error != null)
			{
				OnDisconnect(-1, "error sending data");
			}

			m_ping = (int)((Time.time - m_sentTime)*1000);

			m_wwwSending = null;
		}

		if (m_wwwSending == null && m_outQueue.Count > 0)
		{
			var messages = string.Join(",", m_outQueue.ToArray());

			m_wwwSending = new WWW(m_xhr + "_send", StringToByteArray(string.Format("[{0}]", messages)), m_sendHeader);

			m_sentTime = Time.time;
			m_outQueue.Clear();
		}

		// long poll finished ?
		if (m_wwwPolling != null && m_wwwPolling.isDone)
		{
			if(m_wwwPolling.error != null)
			{
				if (Connected)
				{
					OnEventDisconnect(-1, "net error");
				}
			}
			else
			{
				var response = m_wwwPolling.text;

				if (!Connected)
				{
					if (response.Length > 0 && response[0] == 'o')
					{
						OnEventConnected();
					}
				}
				else
				{
					if (response.Length > 0)
					{
						if (response[0] == 'c')
						{
							var payload = response.Substring(2, response.Length - 4);

							var separatorIdx = payload.IndexOf(',');

							string partCode;
							string partMessage;

							partCode = payload.Substring(0, separatorIdx);
							partMessage = payload.Substring(separatorIdx + 1, payload.Length - separatorIdx - 1);

							OnEventDisconnect(int.Parse(partCode), partMessage.Trim('"'));
						}
						else if (response[0] == 'h')
						{
							//Debug.Log("heartbeat");
						}
						else if (response[0] == 'a')
						{
							var payload = response.Substring(3, response.Length - 6);

							if (OnMessage != null)
								OnMessage(payload);
						}
					}
				}
			}

			if (Connected)
				StartPoll();
		}
	}
	
	public void Connect(string _host)
	{
		if(m_state == ConnectionState.Disconnected)
		{
			var serverId = Random.Range(0, 999);

			var sessionIdRnd = Random.Range(0, 100000000);

			var sessionId = string.Format("{0}.{1}.{2}",
				System.DateTime.UtcNow.ToLongTimeString(), 
				sessionIdRnd,
				SystemInfo.deviceUniqueIdentifier);

			m_xhr = _host + string.Format("{0:000}/{1}/xhr", serverId, GetHashString(sessionId));

			m_state = ConnectionState.Connecting;

			StartPoll();
		}
	}

	public void Disconnect()
	{
		if (m_state != ConnectionState.Disconnected)
		{
			m_state = ConnectionState.Disconnected;

			m_wwwSending = null;
			m_wwwPolling = null;
			m_outQueue.Clear();
		}
	}

	public void SendData(string _payload)
	{
		if (m_state == ConnectionState.Connected)
		{
			//TODO: correct json string escaping
			m_outQueue.Add('"'+_payload+'"');
		}
	}

	private void StartPoll()
	{
		m_wwwPolling = new WWW(m_xhr, new byte[] { 0 });
	}

	private static byte[] GetHash(string _inputString)
	{
		HashAlgorithm algorithm = MD5.Create();
		return algorithm.ComputeHash(Encoding.UTF8.GetBytes(_inputString));
	}

	private static string GetHashString(string _inputString)
	{
		var sb = new StringBuilder();
		foreach (byte b in GetHash(_inputString))
			sb.Append(b.ToString("X2"));

		return sb.ToString();
	}
	
	private void OnEventConnected()
	{
		m_state = ConnectionState.Connected;
		
		if(OnConnect != null)
			OnConnect();
	}
	
	private void OnEventDisconnect(int _code, string _msg)
	{
		m_state = ConnectionState.Disconnected;
		
		if(OnDisconnect != null)
			OnDisconnect(_code,_msg);
	}
	
	private static byte[] StringToByteArray(string str)
	{
		System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
		return enc.GetBytes(str);
	}
}
