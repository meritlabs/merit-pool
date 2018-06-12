# Merit Mining Pool

Mining pool implementation for Merit cryptocurrency based on CoiniumServ.

**CoiniumServ** is a high performance, extremely efficient, platform-agnostic, easy to setup pool server implementation. It features stratum and vanilla services, reward, payment, share processors, vardiff & ban managers, user-friendly embedded web-server & front-end and a full-stack API.

CoiniumServ was created to be used for Coinium.org mining pool network at first hand. You can check [some of pools](https://github.com/bonesoul/CoiniumServ/wiki/Pools) of the pools running CoiniumServ.

This version is heavily modified to support Merit and Merit GUI standarts.

### Features
* __Platform Agnostic__; Unlike other pool-servers, CoiniumServ doesn't dictate platforms and can run on anything including Windows, Linux or MacOS.
* __High Performance__; Designed to be fast & efficient, CoiniumServ can handle dozens of pools together.
* __Modular & Flexible__; Designed to be modular since day one so that you can implement your very own ideas.
* __Free & Open-Source__; Best of all CoiniumServ is open source and free-to-use. You can get it running for free in minutes.
* __Easy to Setup__; Requires very limited cofiguration and 3rd party dependencies.

##### General

* Multiple pools & ports
* Multiple coin daemon connections
* Supports POW (proof-of-work) coins
* Supports POS (proof-of-stake) coins

##### Algorithms

* __Scrypt__, __SHA256d__, __X11__, __X13__, X14, X15, X17, Blake, Fresh, Fugue, Groestl, Keccak, NIST5, Scrypt-OG, Scrypt-N, SHA1, SHAvite3, Skein, Qubit, C11, Cuckoo Cycle

##### Protocols

* Stratum
 * show_message support
 * block template support
 * generation transaction support
 * transaction message (txMessage) support
* Getwork [experimental]

##### Storage Layers

* Hybrid mode (redis + mysql)
* [MPOS](https://github.com/MPOS/php-mpos) compatibility (mysql)

##### Embedded Web Server

* Customizable front-end
* Full stack json-api

##### Addititional Features

* ✔ Vardiff support
* ✔ Ban manager (that can handle miners flooding with invalid shares)
* ✔ Share & Payment processor, Job Manager

##### Running your own pool

Check out [Build Instruction](docs/build.md) and [Running Instruction](docs/running.md) to run your own pool instance.
