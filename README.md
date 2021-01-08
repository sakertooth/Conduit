# Conduit
<p align="center">
  <img width="220" height="220" src="https://vignette.wikia.nocookie.net/minecraft/images/1/1d/ConduitBE.gif/revision/latest/top-crop/width/220/height/220?cb=20200118193901">
</p>

## Introduction
Conduit is an asynchronous Minecraft server scanner. What was once known as SharpScanner,
Conduit is an attempt to fix the fallacies of SharpScanner, with a big factor being memory usage.

### The main issue
Quite simply, SharpScanner was taking up way too much memory. This could be for many reasons, including poor distributed workload and/or not closing unmanaged resources. The codebase was a mess as well, so I decided to start over and create Conduit, a more robust, and efficient scanner for Minecraft servers. 

## Features
  * Supports mulitple types of ranges (/24, from - to, single ip)
  * Memory and CPU efficient
  * Well distributed workload
  * Minimal command-line interface  

## Purpose
This tool is intended to be used to secure or reveal open servers on a [BungeeCord](https://github.com/SpigotMC/BungeeCord) network or networks.  
Tips on securing open BungeeCord backends can be found [here](https://www.spigotmc.org/wiki/firewall-guide/). 

## License
[GPL-3.0](LICENSE)
