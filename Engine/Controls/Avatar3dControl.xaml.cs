using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AvatarControl.xaml
	/// </summary>
	public partial class Avatar3dControl : UserControl
	{
		public Avatar3dControl()
		{
			InitializeComponent();
			Basic3DShapeExample();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			InitRotation();
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
			}
		}

		#region Rotating

		private void InitRotation()
		{
			// Initialize rotation
			RotateTimer = new System.Timers.Timer();
			RotateTimer.Interval = 25;
			RotateTimer.Elapsed += RotateTimer_Elapsed;
			RotateTimer.Start();
		}

		System.Timers.Timer RotateTimer;

		private void RotateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			ControlsHelper.AppInvoke(() =>
			{
				var angle = (AxisAngleRotation.Angle + 2) % 360;
				AxisAngleRotation.Angle = angle;
			});
		}

		#endregion

		public void Basic3DShapeExample()
		{

			// Declare scene objects.
			Viewport3D myViewport3D = new Viewport3D();
			Model3DGroup myModel3DGroup = new Model3DGroup();
			GeometryModel3D myGeometryModel = new GeometryModel3D();
			ModelVisual3D myModelVisual3D = new ModelVisual3D();
			// Defines the camera used to view the 3D object. In order to view the 3D object,
			// the camera must be positioned and pointed such that the object is within view
			// of the camera.
			PerspectiveCamera myPCamera = new PerspectiveCamera();

			// Specify where in the 3D scene the camera is.
			myPCamera.Position = new Point3D(0, 0, 2);

			// Specify the direction that the camera is pointing.
			myPCamera.LookDirection = new Vector3D(0, 0, -1);

			// Define camera's horizontal field of view in degrees.
			myPCamera.FieldOfView = 60;

			// Asign the camera to the viewport
			myViewport3D.Camera = myPCamera;
			// Define the lights cast in the scene. Without light, the 3D object cannot
			// be seen. Note: to illuminate an object from additional directions, create
			// additional lights.
			DirectionalLight myDirectionalLight = new DirectionalLight();
			myDirectionalLight.Color = Colors.White;
			myDirectionalLight.Direction = new Vector3D(-0.61, -0.5, -0.61);

			myModel3DGroup.Children.Add(myDirectionalLight);

			// The geometry specifes the shape of the 3D plane. In this sample, a flat sheet
			// is created.
			MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();

			// Create a collection of normal vectors for the MeshGeometry3D.
			Vector3DCollection myNormalCollection = new Vector3DCollection();
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myNormalCollection.Add(new Vector3D(0, 0, 1));
			myMeshGeometry3D.Normals = myNormalCollection;

			// Create a collection of vertex positions for the MeshGeometry3D.
			Point3DCollection myPositionCollection = new Point3DCollection();
			myPositionCollection.Add(new Point3D(-0.5, -0.5, 0.5));
			myPositionCollection.Add(new Point3D(0.5, -0.5, 0.5));
			myPositionCollection.Add(new Point3D(0.5, 0.5, 0.5));
			myPositionCollection.Add(new Point3D(0.5, 0.5, 0.5));
			myPositionCollection.Add(new Point3D(-0.5, 0.5, 0.5));
			myPositionCollection.Add(new Point3D(-0.5, -0.5, 0.5));
			myMeshGeometry3D.Positions = myPositionCollection;

			// Create a collection of texture coordinates for the MeshGeometry3D.
			PointCollection myTextureCoordinatesCollection = new PointCollection();
			myTextureCoordinatesCollection.Add(new Point(0, 0));
			myTextureCoordinatesCollection.Add(new Point(1, 0));
			myTextureCoordinatesCollection.Add(new Point(1, 1));
			myTextureCoordinatesCollection.Add(new Point(1, 1));
			myTextureCoordinatesCollection.Add(new Point(0, 1));
			myTextureCoordinatesCollection.Add(new Point(0, 0));
			myMeshGeometry3D.TextureCoordinates = myTextureCoordinatesCollection;

			// Create a collection of triangle indices for the MeshGeometry3D.
			Int32Collection myTriangleIndicesCollection = new Int32Collection();
			myTriangleIndicesCollection.Add(0);
			myTriangleIndicesCollection.Add(1);
			myTriangleIndicesCollection.Add(2);
			myTriangleIndicesCollection.Add(3);
			myTriangleIndicesCollection.Add(4);
			myTriangleIndicesCollection.Add(5);
			myMeshGeometry3D.TriangleIndices = myTriangleIndicesCollection;

			// Apply the mesh to the geometry model.
			myGeometryModel.Geometry = myMeshGeometry3D;

			// The material specifies the material applied to the 3D object. In this sample a
			// linear gradient covers the surface of the 3D object.

			// Create a horizontal linear gradient with four stops.
			LinearGradientBrush myHorizontalGradient = new LinearGradientBrush();
			myHorizontalGradient.StartPoint = new Point(0, 0.5);
			myHorizontalGradient.EndPoint = new Point(1, 0.5);
			myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
			myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.25));
			myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.75));
			myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.LimeGreen, 1.0));

			// Define material and apply to the mesh geometries.
			DiffuseMaterial myMaterial = new DiffuseMaterial(myHorizontalGradient);
			myGeometryModel.Material = myMaterial;

			// Apply a transform to the object. In this sample, a rotation transform is applied,
			// rendering the 3D object rotated.
			RotateTransform3D myRotateTransform3D = new RotateTransform3D();
			AxisAngleRotation3D myAxisAngleRotation3d = new AxisAngleRotation3D();
			myAxisAngleRotation3d.Axis = new Vector3D(0, 3, 0);
			myAxisAngleRotation3d.Angle = 40;
			myRotateTransform3D.Rotation = myAxisAngleRotation3d;
			myGeometryModel.Transform = myRotateTransform3D;

			// Add the geometry model to the model group.
			myModel3DGroup.Children.Add(myGeometryModel);

			// Add the group of models to the ModelVisual3d.
			myModelVisual3D.Content = myModel3DGroup;

			//
			myViewport3D.Children.Add(myModelVisual3D);

			// Apply the viewport to the page so it will be rendered.
			Content = myViewport3D;
		}

	}
}
