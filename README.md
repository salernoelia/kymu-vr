# Kymu VR - Pediatric Rehabilitation in Virtual Reality

![Kymu VR Overview](/Assets/Media/Images/kymu-vr-overview.png)

This repository contains the Unity-based Virtual Reality (VR) client for the [Kymu](https://github.com/salernoelia/kymu) telerehabilitation service. Kymu is a Bachelor's Thesis project (June 2025, Zurich University of the Arts) aimed at providing a multidisciplinary telerehabilitation service for pediatric neuromuscular (NMD) physiotherapy. The VR application serves as one of the primary interfaces for children to perform guided therapeutic sessions and engaging exergames at home.

## VR Application Features

*   **Immersive Exercise Environments:** A variety of scenes (e.g., Rowing, Ping Pong, Beach, Pirate) designed to make exercises engaging.
*   **Gamified Therapeutic Activities (Exergames):** Interactive games that incorporate therapeutic movements.
*   **Body Pose Tracking & Validation:** Utilizes Meta Quest's body tracking capabilities (Movement SDK) to assess and guide exercise execution.
    *   **Custom Pose Definition:** Uses ScriptableObjects (`Assets/BodyPoses`) to define target poses and sequences for exercises (e.g., `ArmCenterToAngle`, `Rowing` poses).
    *   **Real-time Feedback:** Scripts in `Assets/Scripts/BodyPoseF/` likely handle pose comparison and provide feedback.
*   **Exercise Result Tracking:** Captures data on exercise performance (e.g., accuracy, duration, detected poses), saved locally (`Assets/ExerciseResults/`) and intended for synchronization with the main Kymu platform (via Supabase).
*   **Designed for Meta Quest 3 and 3s:** Specifically developed and tested for Meta Quest VR headsets.
*   **Audio-Visual Feedback:** Incorporates sound effects and visual cues to enhance immersion and provide performance feedback.
*   **User Interface (UI) for Navigation:** In-VR menus for scene selection and settings (`Assets/Prefabs/UI/Menu.prefab`).

## Tech Stack

*   **Game Engine:** Unity (Version `6000.0.38f1` or as specified in `ProjectSettings/ProjectVersion.txt`)
*   **Target Platform:** Meta Quest (using Meta XR SDKs - Interaction, Movement, Haptics)
*   **Programming Language:** C#
*   **Data Management:**
    *   Local JSON files for exercise results.
    *   (Intended) Supabase integration for cloud data synchronization (`Assets/Scripts/Supabase/SupabaseManager.cs` and NuGet packages).
*   **Assets:** Mix of custom-developed and downloaded 3D models, textures, and audio (see `Assets/Downloaded/` and `Assets/Models/`).
*   **Version Control:** Git

## Getting Started

### Prerequisites

*   **Unity Editor:** Version `6000.0.38f1`. Ensure Android Build Support with OpenJDK and Android SDK & NDK Tools are installed.
*   **Meta Quest Headset:** Meta Quest 3 or Quest 3s.

### Installation & Setup

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/salernoelia/kymu-vr.git
    cd kymu-vr
    ```

2.  **Open the project in Unity:**
    *   Add the cloned `kymu-vr` folder as a project.
    *   Ensure you select the correct Unity Editor version.

3.  **Package Resolution:**
    *   Unity should automatically resolve packages listed in `Packages/manifest.json` and `Packages/packages-lock.json`.
    *   This includes Meta XR SDKs and other dependencies. If there are issues, check the Package Manager console (Window > Package Manager).

4.  **Meta Quest SDK Configuration:**
    *   Verify Meta Quest settings in `Project Settings > XR Plug-in Management`. Oculus should be enabled for Android.
    *   Review settings in `Oculus > Tools > Project Setup Tool` and apply recommended fixes if any.

5.  **Supabase Setup:**
    *   If integrating with a Supabase backend, you'll need to configure API URL and Key in the Inspector when using the `SupabaseManager.cs` Script.

## Project Structure

*   **`Assets/`**: Main project folder.
    *   **`Scenes/`**: Contains different game/exercise environments (e.g., `RowingScene.unity`, `PingPongScene.unity`).
        *   `Development/`: Scenes used for testing specific features like pose recording and UI elements.
    *   **`Scripts/`**: C# scripts for game logic, interactions, and features.
        *   `BodyPoseF/`: Core scripts for body pose definition, tracking, comparison, and exercise result saving.
        *   `Scenes/`: Scripts specific to individual scenes/games.
        *   `UI/`: Scripts for managing menus and scene loading.
        *   `Supabase/`: (Likely) Scripts for interacting with the Supabase backend.
    *   **`Prefabs/`**: Reusable GameObjects, including OVR prefabs for VR rig and interaction elements.
    *   **`BodyPoses/`**: ScriptableObjects defining specific body poses and sequences for exercises.
    *   **`Models/`**, **`Materials/`**, **`Textures/`**, **`Audio/`**: Art and sound assets.
    *   **`Downloaded/`**: Third-party assets from the Unity Asset Store or other sources.
    *   **`ExerciseResults/`**: JSON files storing results from completed exercises.
    *   **`MetaXR/`**, **`Oculus/`**, **`Samples/`**: SDK-related files and examples.

## Key Concepts & Modules

*   **Body Pose System:**
    *   Poses are defined as `BodyPoseScriptableObject` assets.
    *   Scripts like `BodyPoseComparerActiveStateMulti.cs` likely compare the player's live pose (from Meta Movement SDK) against these predefined poses.
    *   `SavePoseFromBody.cs` and `SavePoseSequenceFromBody.cs` suggest tools for creating new pose assets within the editor or at runtime for development.
*   **Exercise Execution & Tracking:**
    *   Each scene represents a different exercise or exergame.
    *   `BodyPoseExerciseTracker.cs` and `BodyPoseExerciseResultSaver.cs` are key for managing the exercise flow and saving performance data.
*   **Scene Management & UI:**
    *   `SceneLoader.cs` handles transitions between different exercise scenes.
    *   `Menu.prefab` provides the in-VR interface for users.

## Current Status & Limitations

*   **Work in Progress:** This is a prototype developed as part of a Bachelor's Thesis.
*   **Accessibility:** User testing (Thesis section 5.4.1) revealed accessibility challenges for users with limited arm mobility (e.g., putting on headset, interacting with in-game items). Further refinement is needed.
*   **Focus:** While functional, the VR application is one component of the larger Kymu service. Its primary role in the thesis was to explore VR's potential for engagement and data capture, rather than being a fully polished, market-ready product.
*   **Data Integration:** Full, robust integration with the Supabase backend and therapist web platform is a larger goal of the Kymu service.
