
/*
 * Copyright 2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import org.vertx.java.core.Handler;
import org.vertx.java.core.buffer.Buffer;
import org.vertx.java.core.http.HttpServer;
import org.vertx.java.core.json.JsonObject;
import org.vertx.java.core.sockjs.SockJSServer;
import org.vertx.java.core.sockjs.SockJSSocket;
import org.vertx.java.platform.Verticle;

import java.util.HashMap;

public class SockJSExample extends Verticle {

    private HashMap<String, SockJSSocket> m_clients = new HashMap<String, SockJSSocket>();

	public void start() {
		HttpServer server = vertx.createHttpServer();
	
		SockJSServer sockServer = vertx.createSockJSServer(server);

		sockServer.installApp(new JsonObject().putString("prefix", "/echo"), new Handler<SockJSSocket>() {
			public void handle(final SockJSSocket sock) {

                m_clients.put(sock.writeHandlerID(),sock);

                sock.endHandler(new Handler<Void>() {
                    public void handle(Void _param) {
                        m_clients.remove(sock.writeHandlerID());
                    }
                });

				sock.dataHandler(new Handler<Buffer>() {
					public void handle(Buffer data) {
                        for(SockJSSocket client: m_clients.values())
                            client.write(data);
					}
				});
			}
		});

		server.listen(9999);
	}
}