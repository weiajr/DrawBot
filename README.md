# DrawBot
This bot is designed to automatically place pixels on v6staging.sys42.net by utilizing ICMP pings. The bot incorporates a signal-to-noise ratio algorithm that determines which "sectors" should be prioritized when drawing, enabling it to draw faster than most people while using only 1/10th the number of pings. Additionally, the bot uses parallelization and concurrency to achieve an impressive ~30k PPS per instance.

## Limitations
The ping class used by this bot doesn't allow for true "fire and forget" pinging. This is because the Debian build of .NET 7 doesn't support the use of raw sockets, which is necessary for this functionality. As a result, I had to fallback to using the built-in ping class, which employs a different method to achieve ICMP pings. If Microsoft ever resolves this issue, I will update the bot accordingly. However, for the time being, it will remain as is.
