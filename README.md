sockjs-unity3d-xhr
==================

sockjs client implementation for unity3D using xhr polling (so this works with unity free license).

==================
Usage
==================

For a more complex example see the chat server/client in nodejs and unity in the source code.
See the https://github.com/sockjs/sockjs-node sockjs documentation for details on the server implementation.

Simple usage example:

```
public class Client : MonoBehaviour {

	public SockjsClient sockjs;
	
	public void Start()
	{
		sockjs.OnMessage += OnMessage;
		sockjs.OnConnect += OnConnect;
	}

	private void OnMessage(string _msg)
	{
		Debug.Log(_msg);
	}

	private void OnConnect()
	{
		Debug.Log("connected");
		
		sockjs.SendData("hello world");
	}
}
```