Задача: написать прокси для grpc сервера, работающий на tcp уровне и выполняющий балансировку.

Порт на котором работает прокси, а также downstraem-сервера указываются в appsettings.json в разделе Proxy.

При балансировке выбирается сервер с наиеньшим количеством активных соединений.