# Product Requirements Document: TunnelFin

**Version:** 1.1
**Date:** January 1, 2026
**Author:** Manus AI

---

## 1. Introduction

### 1.1. Overview

TunnelFin is a native Jellyfin plugin designed to provide a seamless and privacy-respecting media streaming experience by integrating torrents directly into the Jellyfin ecosystem. It will function as an all-in-one solution, enabling users to search for, discover, and stream content from the BitTorrent network with an emphasis on user anonymity. The plugin will achieve this by reimplementing the core functionalities of several established open-source projects, including TorrServer for torrent-to-HTTP streaming, Tribler for decentralized anonymity, AIOStreams for content aggregation, and Gelato for deep Jellyfin integration.

### 1.2. Goals

- **Privacy-First Streaming:** To provide users with a secure and anonymous way to stream torrents by default, leveraging a decentralized onion-routing network.
- **Seamless Integration:** To make torrent-based content appear and behave as native items within the Jellyfin library, including search, metadata, and playback.
- **Unified Experience:** To eliminate the need for separate torrent clients, debrid services, or complex setups by creating a single, self-contained plugin.
- **User Empowerment:** To give users clear choices and control over their privacy, content sources, and streaming quality.

### 1.3. Non-Goals

- **Debrid Service Integration:** This plugin will not support premium debrid services (e.g., Real-Debrid, AllDebrid). The focus is on a pure, decentralized P2P approach.
- **Multi-Server Environments:** The initial version will be designed and optimized for self-hosted, single-user Jellyfin instances.
- **Content Hosting:** The plugin will not host or store any content persistently. It is a streaming-only solution.

## 2. Target Audience

The primary audience for TunnelFin consists of existing Jellyfin users who are:

- **Privacy-Conscious:** Individuals who prioritize anonymity and wish to protect their IP address while accessing content on the BitTorrent network.
- **Convenience-Oriented:** Users who desire an integrated "just-works" solution for streaming torrents directly within their media server, without managing multiple applications.
- **Self-Hosters:** Enthusiasts who run their own media servers and have a technical aptitude for configuring plugins and managing their media libraries.

## 3. Functional Requirements

### 3.1. Core Anonymity Layer

TunnelFin will implement a privacy layer that is wire-compatible with the existing Tribler network [1].

- **IPv8 Protocol Reimplementation:** The plugin will contain a native C# implementation of Tribler's IPv8 peer-to-peer networking protocol, allowing it to participate in the existing network.
- **Onion Routing:** All BitTorrent traffic will be routed through a multi-hop (onion) circuit of other Tribler peers, obscuring the user's true IP address from the swarm.
- **Configurable Hop Count:** Users can configure the number of hops (1-3) for their anonymity circuits, allowing them to balance between speed and privacy.
- **Network Contribution:** The plugin will contribute back to the network by acting as a relay node. The amount of relayed traffic will be proportional to the user's download usage to ensure fairness.
- **Peer Identity:** A persistent cryptographic identity (Ed25519 keypair) will be generated and stored for network participation.

### 3.2. Torrent Streaming Engine

The plugin will manage the entire torrent-to-stream lifecycle.

- **BitTorrent Core:** It will be built upon a robust, existing C# BitTorrent library, such as MonoTorrent [2], to handle the underlying protocol mechanics.
- **Sequential Streaming:** The engine will prioritize downloading torrent pieces sequentially to enable immediate playback, similar to the functionality of TorrServer [3].
- **HTTP Stream Exposure:** Active torrents will be exposed as a local HTTP stream that the Jellyfin player can consume.

### 3.3. Content Discovery & Aggregation

TunnelFin will feature a powerful, built-in search and discovery engine that reimplements the advanced aggregation capabilities of AIOStreams [4]. This engine will be responsible for finding content, filtering out unwanted results, and sorting them according to user preferences before they are presented in the Jellyfin UI.

- **Indexer Aggregation:** The plugin will search for content across multiple sources simultaneously.
  - **Built-in Indexers:** It will include native support for several popular public torrent indexers (e.g., 1337x, Nyaa.si, RARBG).
  - **Custom Indexer Support:** Users can add their own indexers via a generic Torznab-compatible API endpoint.

- **Advanced Filtering Engine:** A comprehensive set of rules will be available to refine search results.
  - **Filter Logic:** Users can define rules as **Required**, **Preferred**, **Excluded**, or **Include (whitelist)**.
  - **Filterable Attributes:** Filters can be applied to a wide range of torrent properties, including Resolution, Quality (e.g., WEB-DL, BluRay), Video/Audio Codecs, Audio Channels, HDR formats, Language, File Size, Seeder Count, and Release Group.
  - **Keyword and Regex:** Support for simple keyword inclusion/exclusion and advanced Regex pattern matching on filenames will be included for ultimate precision.
  - **Conditional Filtering:** The engine will support a full expression language for creating dynamic rules (e.g., "Only exclude 720p if more than 5 results at 1080p exist").

- **Sophisticated Sorting:** Users can define custom, multi-criteria sort orders to rank the filtered results. For example, a user could specify sorting by "Resolution (descending), then Seeders (descending), then File Size (ascending)".

- **Content-Type Profiles:** To accommodate different preferences for different media, users can configure separate and distinct filter and sort profiles for **Movies**, **TV Shows**, and **Anime**.

- **Deduplication:** To ensure a clean result list, the plugin will deduplicate identical torrents from different sources. This will be user-configurable, supporting deduplication by **infohash**, **filename**, and a **smart detection** hash generated from file attributes.

- **Strict Title Matching:** To ensure accuracy, the engine will verify results against TMDB/AniList data, correctly matching movie years, TV season/episode numbers, and handling alternative titles or anime numbering schemes.

### 3.4. Jellyfin Integration

The plugin will integrate deeply with Jellyfin, following the patterns established by Gelato [5].

- **Search Provider:** TunnelFin will register as a search provider, allowing users to find torrented content directly from Jellyfin's search bar.
- **Library Integration:** Discovered content will be presented as native Jellyfin library items, complete with rich metadata.
- **Metadata Fetching:** It will fetch metadata for movies and TV shows from The Movie Database (TMDB) and for anime from AniList or MyAnimeList.
- **Scheduled Tasks:** Users can configure scheduled tasks to automatically sync and import catalogs from their favorite torrent sources.

### 3.5. Privacy Fallback Mechanism

- **Tribler-First Approach:** The plugin will always attempt to source and stream content through the anonymous Tribler network first.
- **User Warning & Consent:** If content is only available on the standard BitTorrent network, the user will be prompted with a clear warning about the privacy implications and must explicitly consent to proceed without protection.

## 4. User Experience (UX) & Interface

### 4.1. Playback & Interaction

- **Color-Coded Play Buttons:** The UI will provide immediate visual feedback on the privacy level of a stream:
  - **Green Play Button:** Indicates the stream will be routed through the anonymous Tribler network.
  - **Orange Play Button:** Indicates the stream is only available on the standard BitTorrent network and will expose the user's IP address.
- **Pre-Stream Availability Check:** The plugin will check for source availability on the Tribler network before presenting play options, preventing dead links.
- **Result Presentation:** Search results will be presented with the best match (based on user-defined sorting) displayed prominently. All other valid, non-duplicate results will be available in an expandable "alternatives" section.
- **Customizable Stream Formatting:** Users can define a custom display format for torrent results using a template system, allowing them to see the most relevant information at a glance (e.g., `🎬 [Quality] | [Resolution] | [Codec] | 💾 [Size] | 👤 [Seeders]`).

### 4.2. In-Stream Experience

- **Stream Health Overlay:** A non-intrusive overlay will display real-time stream health metrics, including peer count, download speed, and buffer status.
- **Smart Stream Switching:** If playback quality degrades significantly, the plugin will offer the user the option to seamlessly switch to a healthier torrent source for the same content.

### 4.3. Configuration

All settings will be managed within a dedicated section of the Jellyfin plugin dashboard.

- **Anonymity Settings:** Configure hop count, enable/disable network contribution (relaying), and manage seeding behavior.
- **Indexer Management:** Enable/disable built-in indexers and add custom Torznab endpoints.
- **Search & Caching:** Configure search result caching duration (defaulting to 5-15 minutes).
- **Logging:** Default to minimal, privacy-respecting logging, with an option to enable verbose logging for troubleshooting.

## 5. Technical Architecture

TunnelFin will be a self-contained, pure C# plugin for Jellyfin. It will not require external services like Docker, though its dependencies (like the .NET runtime) must be met by the host Jellyfin environment.

| Component | Technology | Responsibility |
|---|---|---|
| **Plugin Core** | C# / .NET | Manages all sub-modules, integrates with Jellyfin's plugin API. |
| **Networking Layer** | C# | Native reimplementation of the IPv8 protocol for Tribler network compatibility. |
| **BitTorrent Engine** | C# (MonoTorrent) | Handles torrent downloading, piece selection, and peer communication. |
| **Indexer Manager** | C# | Manages queries to built-in and custom Torznab indexers. |
| **Stream Manager** | C# | Converts downloaded torrent data into a playable HTTP stream. |
| **Configuration** | Jellyfin API | Stores all user settings, including peer identity, in the Jellyfin database. |

## 6. Implementation Phases

Development will proceed in a phased approach, starting with foundational technologies.

1.  **Phase 1: Foundational Protocols:**
    -   Implement the core IPv8 networking and cryptographic primitives in C#.
    -   Achieve successful connection and handshake with the live Tribler network.
    -   Build the basic torrent-to-stream engine using MonoTorrent.

2.  **Phase 2: Core Streaming Functionality:**
    -   Integrate the networking and torrent engines.
    -   Implement basic anonymous streaming through the Tribler network.
    -   Create the `IChannel` provider to play a hardcoded torrent.

3.  **Phase 3: Content Discovery & Integration:**
    -   Implement the indexer manager with support for built-in and custom sources.
    -   Integrate with Jellyfin's search provider API.
    -   Implement the metadata fetching and library integration logic.

4.  **Phase 4: Advanced Features & UX Polish:**
    -   Implement the privacy fallback mechanism and user warnings.
    -   Develop the stream health overlay and smart switching.
    -   Refine the configuration UI and add all user-configurable options.

## 7. Privacy & Security

- **Default Anonymity:** All operations will default to the highest privacy settings.
- **Explicit Consent:** No traffic will be sent over the standard BitTorrent network without explicit, per-session user consent.
- **Minimal Logging:** By default, no personally identifiable information or content titles will be stored in logs.
- **Secure Identity:** The user's Tribler network identity (private key) will be stored securely within Jellyfin's encrypted configuration.

## 8. License

This project will be licensed under the **GNU General Public License v3.0 (GPL-3.0)** to respect the licenses of the open-source projects it draws inspiration and functionality from.

---

## 9. References

[1] The Tribler Project. *Tribler: Privacy enhanced BitTorrent client with P2P content discovery*. [https://github.com/Tribler/tribler](https://github.com/Tribler/tribler)

[2] Alan McGovern. *MonoTorrent: A C#/.NET BitTorrent library*. [https://github.com/alanmcgovern/monotorrent](https://github.com/alanmcgovern/monotorrent)

[3] YouROK. *TorrServer: Torrent stream server*. [https://github.com/YouROK/TorrServer](https://github.com/YouROK/TorrServer)

[4] Viren070. *AIOStreams: One addon to rule them all*. [https://github.com/Viren070/AIOStreams](https://github.com/Viren070/AIOStreams)

[5] lostb1t. *Gelato: Jellyfin Stremio Integration Plugin*. [https://github.com/lostb1t/Gelato](https://github.com/lostb1t/Gelato)
