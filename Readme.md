# retsuko-worker

안정적으로 실시간 웹소켓 연결을 유지하고, 복구해주는 마이크로서비스

redis 에서 `worker` hash에 다음과 같이 현재 연결된 것들을 저장한다

```
HSET worker:store id {symbol}:{interval}
```

그리고 웹소켓으로 데이터를 받을 때 마다 redis에 간단한 message queue로써 retsuko-backend 에 보낼 이벤트들을 쌓는다

```
LPUSH worker:queue {json(candle)}
```

메시지를 받을 때마다 서버쪽에 http 요청을 보내 꺼내라고 시키고 서버쪽에서 `RPOP` 으로 빼온다.