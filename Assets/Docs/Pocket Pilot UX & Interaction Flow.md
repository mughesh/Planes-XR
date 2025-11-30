### **User Experience (UX) & Interaction Flow**

#### **1\. The "Pilot's Clipboard" (Main Menu)**

* **Visual:** A simple holographic clipboard that loosely follows the user's gaze (billboarding).  
* **No Anchors:** It floats 0.5m in front of the user, usable sitting or standing.  
* **Buttons:**  
  * \[PLAY NEXT\] (Big Green Button)  
  * \[LEVEL GRID\] (1, 2, 3...)  
  * \[TUTORIAL\] (?)

#### **2\. The "Launch Protocol" (Level Start Sequence)**

* **State 1: Scan:** System quickly meshes the room (if not already done).  
* **State 2: The Hangar:**  
  * The Clipboard fades out.  
  * A **Slingshot** mesh spawns on the User's **Non-Dominant Wrist**.  
  * The Plane sits in the Slingshot pouch.  
* **State 3: Tension:**  
  * User grabs Plane with **Dominant Hand (Pinch)**.  
  * Haptic vibration increases as they pull back.  
  * Audio: Elastic stretching sound (`Streeeetch...`).  
* **State 4: Release:**  
  * User lets go.  
  * Audio: `Snap!` \+ `Whoosh!`  
  * The Slingshot dissolves. Control transfers immediately to the Dominant Hand (Open Palm).

#### **3\. In-Game Feedback (HUD-less Design)**

* **No 2D HUD:** Keep the screen clean.  
* **Health/Fuel:** Visualized on the plane itself (e.g., smoke if damaged, blinking light if low time).  
* **Directional Cue:** If a coin is behind the user, a small 3D arrow floats near the *plane*, pointing to the coin.  
* **The "Clutch" Signifier:** When user makes a Fist, the Plane's engine sound drops to a low idle, and the trail color changes (e.g., White to Blue).

#### **4\. The "Victory Lap" (Level End)**

* **Trigger:** Last coin collected.  
* **Event:** A **Portal** opens. Ideally placed on a wall (using Scene API) or floating in the largest open space.  
* **Action:** User flies into the portal.  
* **Transition:** Screen wipes White \-\> "Level Complete" Stamp appears on the Clipboard.

