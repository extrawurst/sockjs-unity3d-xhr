using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class SockjsClient {
	
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

	private ConnectionState m_state;
	private string m_xhr;
	private WWW m_wwwSendingObject;
	private WWW m_wwwCurrentSending;
	private float m_sentTime;
	private WWW m_wwwPolling;
	private int m_ping;
	private string m_host;
	private readonly List<string> m_outQueue = new List<string>();
	private static readonly Regex RegexSplitter = new Regex("\",\"");
	private static readonly byte[] PollPostData = {0};
	private static readonly string[] PollHeaders = {};
	private static readonly ASCIIEncoding AsciiEncoding = new ASCIIEncoding();
	private static readonly StringBuilder HashStringBuilder = new StringBuilder();
	private static readonly Hashtable SendHeader = new Hashtable();
	private static readonly string[] SendHeaderStrings = {"Content-Type=application/xml"};

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

	public int AutoPingRefreshMs
	{
		get; set;
	}

	public string Host
	{
		get { return m_host; }
	}

	public SockjsClient()
	{
		if (SendHeader.Count == 0)
			SendHeader["Content-Type"] = "application/xml";
	}

	public void Update()
	{
		if (m_wwwCurrentSending != null && m_wwwCurrentSending.isDone)
		{
			if (m_wwwCurrentSending.error != null)
			{
				OnEventDisconnect(-1, "error sending data");
				Debug.LogError("[sockjs] send error -> disconnect");
			}

			m_ping = (int)((Time.time - m_sentTime)*1000);

			m_wwwCurrentSending = null;
		}

		AutoPingRefresh();

		FlushOutqueue();

		// long poll finished ?
		if (m_wwwPolling != null && m_wwwPolling.isDone)
		{
			if(m_wwwPolling.error != null)
			{
				if (Connected)
				{
					OnEventDisconnect(-1, "net error");
					Debug.LogError("[sockjs] poll error -> disconnect");
				}
				else
				{
					OnEventDisconnect(-1, "connect error");
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
					else
						Debug.LogError("[sockjs] unkown message: " + response);	
				}
				else
				{
					if (response.Length > 0)
					{
						if (response[0] == 'c')
						{
							var payload = response.Substring(2, response.Length - 4);

							var separatorIdx = payload.IndexOf(',');

							string partCode = payload.Substring(0, separatorIdx);
							string partMessage = payload.Substring(separatorIdx + 1, payload.Length - separatorIdx - 1);

							OnEventDisconnect(int.Parse(partCode), partMessage.Trim('"'));
						}
						else if (response[0] == 'h')
						{
							//Debug.Log("heartbeat");
						}
						else if (response[0] == 'a')
						{
							var payload = response.Substring(3, response.Length - 6);

							var messages = RegexSplitter.Split(payload);

							if (OnMessage != null)
							{
								foreach(var msg in messages)
									OnMessage(DecodeMsg(msg));
							}
						}
						else
						{
							Debug.LogError("[sockjs] unkown message: " + response);
						}
					}
				}
			}

			if (Connected)
				StartPoll();
			else
				m_wwwPolling = null;
		}
	}

	private void AutoPingRefresh()
	{
		if (AutoPingRefreshMs > 0)
		{
			var lastSent = (int)((Time.time - m_sentTime) * 1000.0f);
			if (lastSent > AutoPingRefreshMs)
			{
				SendData("");
			}
		}
	}

	public void Connect(string _host)
	{
		if(m_state == ConnectionState.Disconnected)
		{
			m_host = _host;

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
			OnEventDisconnect(0, "user disconnect");

			if (m_wwwCurrentSending != null)
				m_wwwCurrentSending.Dispose();

			if (m_wwwPolling != null)
				m_wwwPolling.Dispose();

			m_wwwCurrentSending = null;
			m_wwwPolling = null;

			m_outQueue.Clear();
		}
	}

	public void SendData(string _payload, bool _tryFlush=false)
	{
		if (m_state == ConnectionState.Connected)
		{
			var escapedMsg = '"' + EncodeMsg(_payload) + '"';

			m_outQueue.Add(escapedMsg);
		}

		if (_tryFlush)
			FlushOutqueue();
	}

	private static string DecodeMsg(string _msg)
	{
		return _msg.Replace("\\\\", "\\").Replace("\\\"", "\"");
	}

	private static string EncodeMsg(string _payload)
	{
		return _payload.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private void FlushOutqueue()
	{
		if (m_wwwCurrentSending == null && m_outQueue.Count > 0)
		{
			var messages = string.Join(",", m_outQueue.ToArray());

			var postData = StringToByteArray(string.Format("[{0}]", messages));

			m_wwwSendingObject = new WWW(m_xhr + "_send", postData, SendHeader);
				
			m_wwwCurrentSending = m_wwwSendingObject;

			m_sentTime = Time.time;
			m_outQueue.Clear();
		}
	}

	private void StartPoll()
	{
		if (m_wwwPolling == null)
			m_wwwPolling = new WWW(m_xhr, PollPostData);
		else
			m_wwwPolling.InitWWW(m_xhr, PollPostData, PollHeaders);
	}

	private static byte[] GetHash(string _inputString)
	{
		HashAlgorithm algorithm = MD5.Create();
		return algorithm.ComputeHash(Encoding.UTF8.GetBytes(_inputString));
	}

	private static string GetHashString(string _inputString)
	{
		HashStringBuilder.Length = 0;
		foreach (byte b in GetHash(_inputString))
			HashStringBuilder.Append(b.ToString("X2"));

		return HashStringBuilder.ToString();
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
	
	private static byte[] StringToByteArray(string _str)
	{
		var enc = AsciiEncoding;
		return enc.GetBytes(_str);
	}
}
