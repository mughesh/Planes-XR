### **Game Design Document (GDD)**

**Project Name:** Pocket Pilot (Working Title)   
**Genre:** Casual / Mixed Reality / Flight Action   
**Target Platform:** Meta Quest 3 / 3S (Passthrough Required)   
**Competition Category:** Best Casual Game \+ Hand Interactions

#### **1\. The Elevator Pitch**

"Room Scale Ace" turns your living room into a stunt course. Reclaiming the childhood nostalgia of pretending your hand is a plane, players physically run, duck, and weave through their own furniture to collect coins and enter portals. No joysticks, no complex buttons—just your hand, a slingshot, and the open air of your living room.

#### **2\. Core Gameplay Loop**

1. **Launch:** Pull back the virtual slingshot on your wrist to launch the plane.  
2. **Fly:** Use direct hand-tilts to steer the plane through the physical room (Passthrough).  
3. **Collect:** Gather procedurally placed coins around real-world obstacles (tables/chairs).  
4. **Exit:** Fly into the "Rift Portal" to finish the level before fuel (time) runs out.

#### **3\. The Control Scheme (The "Free Flight" System)**

* **The Pilot (Right Hand):** The plane is a "magnetic extension" of the hand.  
  * **Roll:** Tilt palm Left/Right (Banks the plane \+ Coordinated Turn).  
  * **Pitch:** Tilt fingers Up/Down (Nose Up/Down).  
  * **Throttle:** Automatic Cruise (Constant speed).  
  * **Boost:** Punch forward gesture (Short speed burst).  
* **The Clutch (Fatigue Management):**  
  * **Action:** Make a **Fist**.  
  * **Result:** "Loiter Mode." The plane ignores input and circles in place. Used to rest the arm or reposition the body without crashing.  
* **Safety Net:** If hand tracking is lost, the plane automatically enters Loiter Mode.

#### **4\. Level Design Strategy (Procedural/MRUK)**

Levels are not hard-coded scenes; they are rules sets applied to the user's room using Meta XR Scene SDK.

* **The Floor Deck:** Coins spawn under tables/chairs (forcing crouching).  
* **The Ceiling Run:** Coins spawn high up (forcing reaching).  
* **The Slalom:** Coins spawn in a curve around the main open space.

#### **5\. Scoring System**

* **3 Stars:** All Coins collected \+ Finished within "Target Time" (calculated based on room volume).  
* **2 Stars:** All Coins collected (Any time).  
* **1 Star:** Level finished (Portal entered) but missed coins.

