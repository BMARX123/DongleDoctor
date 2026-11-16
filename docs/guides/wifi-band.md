# Your game stream isn't on 6 GHz

The Frame's wireless adapter can stream over 6 GHz, the newest Wi-Fi band. Few devices use it yet, so it's usually much quieter than 5 GHz, and it allows a wider channel. Your Frame is streaming over 5 GHz (or 2.4 GHz) instead, where it shares the air with the Wi-Fi networks around you.

## Why this happens

Your Frame chooses the band, not your PC. The Frame uses one Wi-Fi chip for two jobs: the link to the wireless adapter, and its own connection to your home Wi-Fi. When the Frame's home Wi-Fi connection is on 6 GHz, the Frame moves the adapter's link down to 5 GHz to make room.

This is the most common cause, and it's easy to fix.

## What to do

1. **On your Frame, open its Wi-Fi settings** and look at which network it's connected to.
2. **Connect it to your router's 5 GHz or 2.4 GHz network instead of 6 GHz.** Many routers show these as separate names, such as "MyHome-5G" and "MyHome-6G". Pick the 5 GHz one.
3. **Wait a few seconds.** The Frame moves the adapter's link to 6 GHz by itself. If it doesn't, restart SteamVR.
4. **Run DongleDoctor again** to check that your Frame is now on 6 GHz.

### If your router uses one name for every band

Some routers put 2.4, 5 and 6 GHz under a single network name and choose the band for you. Your Frame may still end up on 6 GHz. In your router's settings, look for an option to give each band its own name (often called "band steering" or "Smart Connect"), then connect your Frame to the 5 GHz name.

## If it still isn't on 6 GHz

In some countries, the Frame's software doesn't turn on 6 GHz yet, even where 6 GHz Wi-Fi is allowed. If your Frame's home Wi-Fi isn't on 6 GHz and the link still stays on 5 GHz or 2.4 GHz, this may be why. Valve is tracking reports like this in the [SteamOS issue tracker](https://github.com/ValveSoftware/SteamOS/issues/2909). Adding your country and setup there helps get it fixed.
