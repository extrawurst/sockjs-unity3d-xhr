using System;
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
	private byte[] m_lastSentBytes = new byte[1];
	private float m_sentTime;
	private WWW m_wwwPolling;
	private float m_pollStartTime;
	private int m_ping;
	private string m_host;
	private int m_currentPollErrors;
	private int m_currentSendErrors;
	private int m_errorLimit = 3;
	private readonly List<string> m_outQueue = new List<string>();
	private static readonly Regex RegexSplitter = new Regex("\",\"");
	private static readonly byte[] PollPostData = {0};
	private static readonly string[] PollHeaders = {};
	private static readonly ASCIIEncoding AsciiEncoding = new ASCIIEncoding();
	private static readonly StringBuilder HashStringBuilder = new StringBuilder();
	private static readonly Dictionary<string, string> SendHeader = new Dictionary<string, string>();
	//Note: unity pre 4.5 needs the following line instead of the Dictionary
	//private static readonly Hashtable SendHeader = new Hashtable();
	private int m_pollTimeoutConnectingSecs = 5;
	private int m_longPollTimeOutSec = 60;

	public ConnectionState State
	{
		get { return m_state; }
	}
	
	public bool Connected
	{
		get { return m_state == ConnectionState.Connected; }
	}

	public bool Connecting
	{
		get { return m_state == ConnectionState.Connecting; }
	}

	public int Ping
	{
		get { return m_ping; }
	}

	public int AutoPingRefreshMs
	{
		get; set;
	}

	public int ErrorLimit
	{
		get { return m_errorLimit; }
		set { m_errorLimit = value; }
	}

	/// <summary>
	/// in seconds
	/// </summary>
	public int PollTimeoutConnecting
	{
		get { return m_pollTimeoutConnectingSecs; }
		set { m_pollTimeoutConnectingSecs = value; }
	}

	/// <summary>
	/// in seconds
	/// </summary>
	public int LongPollTimeOut
	{
		get { return m_longPollTimeOutSec; }
		set { m_longPollTimeOutSec = value; }
	}

	public string Host
	{
		get { return m_host; }
	}

	private double CurrentPollTimeout {
		get { return Connected ? m_longPollTimeOutSec : m_pollTimeoutConnectingSecs; }
	}

	public SockjsClient()
	{
		if (SendHeader.Count == 0)
			SendHeader["Content-Type"] = "application/xml";
	}

	public void Update()
	{
		UpdateSending();

		AutoPingRefresh();

		FlushOutqueue();

		UpdatePolling();
	}

	public bool UpdateSending()
	{
		if (m_wwwCurrentSending != null && m_wwwCurrentSending.isDone)
		{
			if (m_wwwCurrentSending.error != null)
			{
				m_currentSendErrors++;

				Debug.LogError(string.Format("[sjs] send err: '{0}' to: '{1}' bytecount: {2} count: {3}", m_wwwCurrentSending.error,
					m_wwwCurrentSending.url, m_lastSentBytes.Length, m_currentSendErrors));

				if (m_currentSendErrors > m_errorLimit)
					OnEventDisconnect(-1, "send error");
				else
					StartSend(m_lastSentBytes);
			}
			else
			{
				m_currentSendErrors = 0;

				m_ping = (int) ((Time.time - m_sentTime)*1000);

				m_wwwCurrentSending = null;

				m_lastSentBytes = null;

				return true;
			}
		}

		return false;
	}

	private void UpdatePolling()
	{
		if (m_wwwPolling != null && m_wwwPolling.isDone)
		{
			if (m_wwwPolling.error != null)
			{
				m_currentPollErrors++;

				Debug.LogError(string.Format("[sjs] poll err: '{0}' to: '{1}' count: {2}", m_wwwPolling.error, m_wwwPolling.url,
					m_currentPollErrors));

				if (m_currentPollErrors > m_errorLimit)
				{
					OnEventDisconnect(-1, Connected ? "net error" : "connect error");
				}
			}
			else
			{
				m_currentPollErrors = 0;

				var response = m_wwwPolling.text;

				if (!Connected)
				{
					if (response.Length > 0 && response[0] == 'o')
					{
						OnEventConnected();
					}
					else
					{
						Debug.LogError("[sockjs] unkown message: " + response);

						OnEventDisconnect(-1, "poll error while connecting: unknown message");
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
							response = response.TrimEnd();
							var payload = response.Substring(3, response.Length - 5);

							var messages = RegexSplitter.Split(payload);

							if (OnMessage != null)
							{
								foreach (var msg in messages)
									OnMessage(DecodeMsg(msg));
							}
						}
						else
						{
							Debug.LogError("[sockjs] unkown message: " + response);

							OnEventDisconnect(-1, "poll error: unknown message");
						}
					}
				}
			}

			if (Connected || Connecting)
				StartPoll();
			else
				m_wwwPolling = null;
		}
		else if (m_wwwPolling != null && (Time.time - m_pollStartTime > CurrentPollTimeout))
		{
			Debug.LogError("[sjs] timeout: " + Connected);
			OnEventDisconnect(-1, Connected ? "net error" : "connect error");
		}
	}

	private void AutoPingRefresh()
	{
		if (AutoPingRefreshMs > 0)
		{
			var lastSent = (int) ((Time.time - m_sentTime)*1000.0f);
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
				DateTime.UtcNow.ToLongTimeString(), 
				sessionIdRnd,
				SystemInfo.deviceUniqueIdentifier);

			m_xhr = _host + string.Format("{0:000}/{1}/xhr", serverId, GetHashString(sessionId));

			m_state = ConnectionState.Connecting;

			m_currentPollErrors = 0;
			m_currentSendErrors = 0;

			StartPoll();
		}
	}

	public void Disconnect()
	{
		if (m_state != ConnectionState.Disconnected)
		{
			OnEventDisconnect(0, "user disconnect");

			//if (m_wwwCurrentSending != null)
			//	m_wwwCurrentSending.Dispose();
			//
			//if (m_wwwPolling != null)
			//	m_wwwPolling.Dispose();

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

			char[] msg = string.Format("[{0}]", messages).ToCharArray();

			Array.Resize(ref m_lastSentBytes, AsciiEncoding.GetByteCount(msg));

			AsciiEncoding.GetBytes(msg,0,msg.Length,m_lastSentBytes,0);

			StartSend(m_lastSentBytes);

			m_outQueue.Clear();
		}
	}

	private void StartSend(byte[] _data)
	{
		//Debug.LogError("[sjs] sending: " + _data.Length);

		m_wwwSendingObject = new WWW(m_xhr + "_send", _data, SendHeader);

		m_wwwCurrentSending = m_wwwSendingObject;

		m_sentTime = Time.time;
	}

	private void StartPoll()
	{
		if (m_wwwPolling == null)
			m_wwwPolling = new WWW(m_xhr, PollPostData);
		else
			m_wwwPolling.InitWWW(m_xhr, PollPostData, PollHeaders);

		m_pollStartTime = Time.time;
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
}
