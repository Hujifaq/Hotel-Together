# Lobby art assets

TravelPosterAtlas.png was generated with the built-in imagegen tool.

Prompt: Original 2×2 travel poster atlas for a playful stylized hotel game. Retro screen-print flat graphic style with grainy paper, dark teal, cream, mustard and terracotta. Four illustrations: seaside sunset with palm, terracotta hotel, tropical foliage and geometric arches, sailboat at sunset. No text, people, logos or watermark.

LobbyRugPattern.png and TealPanelPattern.png are original procedural patterns authored by HotelLobbyArtDirection.cs. The tool regenerates its own two procedural textures when applying the theme. The imported HotelScene.glb remains intact.

Existing props use scoped scene materials rather than changing shared imported GLB materials in other rooms. Architectural finishes use saved mesh partitions retaining UVs/normals and source materials outside the lobby bounds. Atmosphere profile components are saved as real Unity sub-assets.
