using System.Collections.Generic;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{

	/*

	AI Prompt

Analyze image to identify and extract information about objects within image. Given an input image, perform the following:

1. **Object Detection and Identification:**
   - Analyze the image and detect all distinct objects present.
   - For each object, assign a descriptive name (e.g., "Red Apple", "Wooden Chair", "Golden Retriever Dog").

2. **Selection Data Extraction:**
   - For each identified object, extract the selection data that outlines its borders.
   - The selection data should be a collection of points (coordinates) that trace the perimeter of the object.
   - Ensure the selection data is precise enough to recreate the object's outline within a graphical interface.

3. **Output Formatting:**
   - Compile the results into a JSON list where each entry contains:
     - `"objectName"`: The name of the object.
     - `"selectionData"`: An array of points representing the object's border. Each point should have `"x"` and `"y"` coordinates.
   - Example format:

     ```json
     [
       {
         "objectName": "Tree",
         "selectionData": [
           {"x": 100, "y": 150},
           {"x": 105, "y": 148},
           ...
         ]
       },
       {
         "objectName": "Car",
         "selectionData": [
           {"x": 200, "y": 250},
           {"x": 205, "y": 248},
           ...
         ]
       }
     ]
     ```

**Output:**

- Return properly formatted and syntactically correct JSON.

**Additional Notes:**

- Assume the coordinate system for the selection data aligns with the image's pixel dimensions.
- The selection data should be accurate to allow high-quality manipulation of objects within the `InkCanvas`.

	*/

	public class ObjectSelection
	{
		public string ObjectName { get; set; }
		public List<Point> SelectionData { get; set; }
	}
}
