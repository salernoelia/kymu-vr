# Kymu VR

![Kymu VR Overview](/Assets/Media/Images/kymu-vr-overview.png)

VR therapy games for kids with neuromuscular conditions. Makes physical therapy fun through immersive environments like rowing, ping pong, and pirate adventures.

Part of the [Kymu](https://github.com/salernoelia/kymu) rehabilitation platform.

## What it does

- **Fun therapy games:** Rowing, ping pong, beach scenes that make exercises engaging
- **Body tracking:** Uses Meta Quest's movement tracking to guide and validate exercises  
- **Real-time feedback:** Audio/visual cues when you're doing movements correctly
- **Progress tracking:** Saves exercise performance data locally
- **Custom poses:** Define therapeutic movements as game targets

## Setup

**Requirements:**
- Unity `6000.0.38f1` with Android build support
- Meta Quest 3 or 3s

**Install:**
```bash
git clone https://github.com/salernoelia/kymu-vr.git
cd kymu-vr
```

Open in Unity, let packages resolve automatically. Configure Meta Quest settings in Project Settings > XR.

## Key Files

- `Assets/Scenes/` - Different therapy environments
- `Assets/BodyPoses/` - Exercise pose definitions  
- `Assets/Scripts/BodyPoseF/` - Pose tracking and validation
- `Assets/ExerciseResults/` - Saved performance data

## Status

Prototype from Bachelor's Thesis (June 2025, ZHdK). Functional but needs accessibility improvements based on user testing with kids who have limited mobility.

**Tech:** Unity, C#, Meta XR SDKs, Supabase integration
