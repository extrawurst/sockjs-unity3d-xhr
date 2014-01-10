import std.stdio;
import vibe.d;

import sockjs.sockjs;

Connection[string] clients;

static this()
{
	SockJS.Options opt = {
	heartbeat_delay : 4_000,
	prefix : "/echo/"
	};

	auto sjs = SockJS.createServer(opt);

	sjs.onConnection = (Connection conn) {
		writefln("new conn: %s", conn.remoteAddress);

		clients[conn.userId] = conn;

		conn.onData = (string message) {

			writefln("msg: %s", message);

			foreach(client; clients)
				client.write(message);
		};

		conn.onClose = () {
			writefln("closed conn: %s", conn.remoteAddress);

			clients.remove(conn.userId);
		};
	};

	auto router = new URLRouter;
	router.any("*", &sjs.handleRequest);

	auto settings = new HTTPServerSettings;
	settings.port = 9999;
	listenHTTP(settings, router);
}