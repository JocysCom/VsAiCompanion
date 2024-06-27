using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Text;
using JocysCom.VS.AiCompanion.Engine.Speech;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AvatarControl.xaml
	/// </summary>
	public partial class AvatarControl : UserControl, INotifyPropertyChanged
	{
		public AvatarControl()
		{
			InitializeComponent();
			CreateBackgroundAnimations();
			CreateGlowAnimation();
			CreateVisemePathDictionary();
			mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
			mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
			// Lips storyboard and animations.
			storyboardLips.CurrentTimeInvalidated += StoryboardLips_CurrentTimeInvalidated;
			storyboardLips.Completed += StoryboardLips_Completed;
			StoryboardLipsSet(animation_TXT, LetterAnimationTextBlock, "Text");
			StoryboardLipsSet(animation_PTH, PathAnimationTextBlock, "Text");
			StoryboardLipsSet(animation_CHI, JawGrid, "Height");
			StoryboardLipsSet(animation_BAR, AnimationProgressBar, "Width");
			StoryboardLipsSet(animation_EYE, IrisAnimationTextBlock, "Text");
			// Uploaded and Downloaded storyboard and animations.
			storyboardUploaded.Completed += StoryboardUploadedCompleted;
			storyboardDownloaded.Completed += StoryboardDownloadedCompleted;
			SetLipsMeshGeometry3D();
			SetLipsMeshGeometry3DUsingPathData(MPath_0.Data);
			pathNow = MPath_0;
		}

		int LipAnimationFrames = 6; // Min 1.
		int LipGeometryDivisions = 9; // Min 2.
									  // Audio file and data.
		public string AudioPath; // @"D:\Projects\Jocys.com GitHub\VsAiCompanion\Engine\Resources\Images\AudioDemo.wav";
		AudioFileInfo AudioData = new AudioFileInfo();
		//string audioText = "AI Companion is a free open source project for people who have an OpenAI API GPT four subscription and run OpenAI on their local machine on premises or on Azure Cloud";
		MediaPlayer mediaPlayer = new MediaPlayer();
		// Spark images.
		BitmapImage bitmapImageYellow = new BitmapImage(new Uri("pack://application:,,,/JocysCom.VS.AiCompanion.Engine;component/Resources/Images/SparkYellow.png"));
		BitmapImage bitmapImageBrown = new BitmapImage(new Uri("pack://application:,,,/JocysCom.VS.AiCompanion.Engine;component/Resources/Images/SparkBrown.png"));
		BitmapImage bitmapImageBlue = new BitmapImage(new Uri("pack://application:,,,/JocysCom.VS.AiCompanion.Engine;component/Resources/Images/SparkBlue.png"));
		BitmapImage bitmapImageLip0 = new BitmapImage(new Uri("pack://application:,,,/JocysCom.VS.AiCompanion.Engine;component/Resources/Images/LipTop0.png"));
		BitmapImage bitmapImageLip1 = new BitmapImage(new Uri("pack://application:,,,/JocysCom.VS.AiCompanion.Engine;component/Resources/Images/LipTop1.png"));
		// Animations.
		StringAnimationUsingKeyFrames animation_TXT = new StringAnimationUsingKeyFrames();
		StringAnimationUsingKeyFrames animation_PTH = new StringAnimationUsingKeyFrames();
		DoubleAnimationUsingKeyFrames animation_CHI = new DoubleAnimationUsingKeyFrames();
		DoubleAnimationUsingKeyFrames animation_BAR = new DoubleAnimationUsingKeyFrames();
		StringAnimationUsingKeyFrames animation_EYE = new StringAnimationUsingKeyFrames();
		DoubleAnimationUsingKeyFrames animation_GLW = new DoubleAnimationUsingKeyFrames();
		// Storyboards for animations.
		Storyboard storyboardGlow = new Storyboard();
		Storyboard storyboardBackground = new Storyboard();
		Storyboard storyboardLips = new Storyboard();
		Storyboard storyboardUploaded = new Storyboard();
		Storyboard storyboardDownloaded = new Storyboard();
		// Dictionaries.
		Dictionary<int, Path> visemePathDictionary = new Dictionary<int, Path>();
		// Current path.
		Path pathNow = new Path();

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AudioCollection.CollectionChanged += AudioCollection_CollectionChanged;
				AppHelper.InitHelp(this);
			}
		}

		// Audio collection.
		private ObservableCollection<(string, AudioFileInfo)> _audioCollection = new ObservableCollection<(string, AudioFileInfo)>();
		public ObservableCollection<(string, AudioFileInfo)> AudioCollection
		{
			get => _audioCollection;
			set
			{
				_audioCollection = value;
				OnPropertyChanged(nameof(AudioCollection));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		// On new item added.
		private void AudioCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			AudioCollectionTextBlock.Text = AudioCollection.Count > 0 ? AudioCollection.Count.ToString() : string.Empty;
			if (e.Action == NotifyCollectionChangedAction.Add) { if (mediaPlayer.Source == null) { OpenAudioFile(); } }
		}

		private void MediaPlayer_MediaEnded(object sender, EventArgs e) { if (AudioCollection.Count > 0) { OpenAudioFile(); } else { MediaPlayerEndedState(); }; }
		private void StoryboardLips_Completed(object sender, EventArgs e) { StoryboardLipsCompletedState(); }

		private void MediaPlayer_MediaButtonPlay(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (!string.IsNullOrEmpty(AudioPath) && AudioData != null) { AudioCollection.Add((AudioPath, AudioData)); }
		}

		public void OpenAudioFile()
		{
			AudioPath = AudioCollection[0].Item1;
			AudioData = AudioCollection[0].Item2;
			AudioCollection.RemoveAt(0);
			// Set mediaPlayer.Source to null for MediaPlayer_MediaOpened to work.
			AnimationAndMediaStop();
			try { mediaPlayer.Open(new Uri(AssemblyInfo.ExpandPath(AudioPath))); }
			catch (Exception ex) { MessageBox.Show($"Error playing audio: {ex.Message}"); }
		}

		// Extract audio file duration in ms and start animation calculations (when completed, audio and lip animation will play automatically).
		private void MediaPlayer_MediaOpened(object sender, EventArgs e)
		{
			var viseme0 = AudioData.Viseme[0].VisemeId + AudioData.Viseme[1].VisemeId + AudioData.Viseme[2].VisemeId;
			if (mediaPlayer.NaturalDuration.HasTimeSpan)
			{
				if (AudioData.Viseme.IsNullOrEmpty() || viseme0 == 0)
				{
					if (AudioData.Boundaries.IsNullOrEmpty())
					{
						CreateLipAnimationFromTextString(AudioData, mediaPlayer.NaturalDuration.TimeSpan);
					}
					else
					{
						CreateLipAnimationFromWordBoundaries(AudioData);
					}
				}
				else
				{
					CreateLipAnimationFromVisemeDictionary(AudioData);
				}
				MediaPlayingState();
				mediaPlayer.Play();
				storyboardLips.Begin();
			}
		}

		private void MediaPlayer_MediaButtonStop(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			AudioCollection.Clear();
			AnimationAndMediaStop();
		}

		private void MediaPlayingState()
		{
			MediaButtonPlay.Visibility = Visibility.Collapsed;
			MediaButtonStop.Visibility = Visibility.Visible;
			AnimationBar.Visibility = Visibility.Visible;
		}

		private void MediaPlayerEndedState()
		{
			mediaPlayer.Close();
			MediaButtonStop.Visibility = Visibility.Collapsed;
			MediaButtonPlay.Visibility = Visibility.Visible;
		}

		private void StoryboardLipsCompletedState()
		{
			AnimationBar.Visibility = Visibility.Collapsed;
			MouthPath.Data = MPath_0.Data;
			SetLipsMeshGeometry3DUsingPathData(MPath_0.Data);
		}

		public void AnimationAndMediaStop()
		{
			// Media.
			mediaPlayer.Stop();
			MediaPlayerEndedState();
			// Animation.
			storyboardLips.Seek(TimeSpan.Zero);
			storyboardLips.Stop();
			animation_CHI.KeyFrames.Clear();
			animation_TXT.KeyFrames.Clear();
			animation_PTH.KeyFrames.Clear();
			animation_BAR.KeyFrames.Clear();
			StoryboardLipsCompletedState();
		}

		public void PlayMessageSentAnimation()
		{
			CreateSparkUpOrDownAnimation(storyboardUploaded, SparksBlueCanvas, storyboardUploadedCanvas);
		}

		public void PlayMessageReceivedAnimation()
		{
			CreateSparkUpOrDownAnimation(storyboardDownloaded, SparksYellowCanvas, storyboardDownloadedCanvas);
		}

		private void StoryboardUploadedCompleted(object sender, EventArgs e)
		{
			storyboardUploaded.Stop();
			storyboardUploaded.Children.Clear();
			storyboardUploadedCanvas.Children.Clear();
		}
		private void StoryboardDownloadedCompleted(object sender, EventArgs e)
		{
			storyboardDownloaded.Stop();
			storyboardDownloaded.Children.Clear();
			storyboardDownloadedCanvas.Children.Clear();
		}

		private void StoryboardLipsSet(Timeline animation, UIElement element, string property)
		{
			Storyboard.SetTarget(animation, element);
			Storyboard.SetTargetProperty(animation, new PropertyPath(property));
			storyboardLips.Children.Add(animation);
		}

		public void PlayGlowAnimation()
		{
			animation_GLW.Completed -= storyboardGlowAnimationStop;
			animation_GLW.Completed += storyboardGlowAnimationContinue;
			storyboardGlow.Begin();
		}
		public void StopGlowAnimation()
		{
			animation_GLW.Completed -= storyboardGlowAnimationContinue;
			animation_GLW.Completed += storyboardGlowAnimationStop;
		}

		private void storyboardGlowAnimationStop(object sender, EventArgs e)
		{
			storyboardGlow.Seek(TimeSpan.Zero);
			storyboardGlow.Stop();
		}

		private void storyboardGlowAnimationContinue(object sender, EventArgs e)
		{
			storyboardGlow.Seek(TimeSpan.Zero);
			storyboardGlow.Begin();
		}

		private void CreateSparkUpOrDownAnimation(Storyboard storyboard, Canvas pathCanvas, Canvas animationCanvas)
		{
			if (storyboard.Children.Count() > 0) { return; }

			var sizeMax = 100;
			var duration = pathCanvas == SparksYellowCanvas ? 1500 : 1900;
			var random = new Random();

			foreach (Path path in pathCanvas.Children)
			{
				var startRandom = TimeSpan.FromMilliseconds(random.Next(0, duration));

				// Create Grid for Image ("spark").
				var grid = new Grid
				{
					Height = sizeMax,
					Width = sizeMax,
					Margin = new Thickness(-sizeMax / 2, -sizeMax / 2, 0, 0),
				};
				animationCanvas.Children.Add(grid);

				// Create Grid moving along the path animation.
				var animationPath = new MatrixAnimationUsingPath
				{
					BeginTime = startRandom,
					Duration = TimeSpan.FromMilliseconds(duration),
					PathGeometry = PathGeometry.CreateFromGeometry(path.Data),
				};
				Storyboard.SetTarget(animationPath, grid);
				Storyboard.SetTargetProperty(animationPath, new PropertyPath("(UIElement.RenderTransform).(MatrixTransform.Matrix)"));
				storyboard.Children.Add(animationPath);

				// Create Image.
				var image = new Image
				{
					Height = 0,
					Width = 0,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Source = pathCanvas == SparksYellowCanvas ? bitmapImageYellow : bitmapImageBlue,
				};
				grid.Children.Add(image);

				// Create Image size animation.
				var sizeMaxR = random.Next(sizeMax * 50, sizeMax * 70) / 100;

				foreach (string property in new[] { "Width", "Height" })
				{
					var animationSize = new DoubleAnimationUsingKeyFrames { BeginTime = startRandom, };

					if (pathCanvas == SparksYellowCanvas)
					{
						animationSize.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.Zero));
						animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration / 5)));
						animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration)));
					}
					else if (pathCanvas == SparksBlueCanvas)
					{
						animationSize.KeyFrames.Add(new DiscreteDoubleKeyFrame(sizeMaxR, TimeSpan.Zero));
						animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration / 2)));
						animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(duration)));
					}
					Storyboard.SetTarget(animationSize, image);
					Storyboard.SetTargetProperty(animationSize, new PropertyPath(property));
					storyboard.Children.Add(animationSize);
				}
			}
			storyboard.Begin();
		}

		// VisemeID, Text, Path, Duration.
		private void CreateLipAnimationFromVisemeDictionary(AudioFileInfo audioData)
		{
			// List<(letter, Path, timeEnd)>.
			var lipAnimationList = new List<(string, Path, double)>();
			foreach (var item in audioData.Viseme)
			{
				if (visemePathDictionary.ContainsKey(item.VisemeId))
				{
					lipAnimationList.Add((item.VisemeId.ToString(), visemePathDictionary[item.VisemeId], item.Offset));
				}
			}
			CreateLipAnimationKeys(lipAnimationList, audioData.Shapes);
		}

		//"Boundaries":[{"ResultId":"0840aab42c5244a2951f5072e03c4e0b","AudioOffset":125,"Duration":"00:00:00.1500000","TextOffset":209,"WordLength":2,"Text":"Na","BoundaryType":0},]}
		private void CreateLipAnimationFromWordBoundaries(AudioFileInfo audioData)
		{
			// List<(letter, Path, duration)>.
			var letterPathDictionary = GetLetterPathDictionary(CharacterVisemeList);
			// List<(letter, Path, timeEnd).
			var lipAnimationList = new List<(string, Path, double)>();
			foreach (var word in audioData.Boundaries)
			{
				var timeStart = word.AudioOffset;
				var letterDuration = word.Duration.TotalMilliseconds / (double)word.WordLength;
				lipAnimationList.Add((" ", letterPathDictionary[" "].Item1, timeStart));
				for (int i = 0; i < word.WordLength; i++)
				{
					// "\u016B" > "ū" > "u".
					var letter = Filters.FoldToASCII(Regex.Unescape(word.Text[i].ToString()).ToLower());
					var timeEnd = timeStart + letterDuration * (i + 1);
					if (letterPathDictionary.ContainsKey(letter))
					{
						lipAnimationList.Add((word.Text[i].ToString(), letterPathDictionary[letter].Item1, timeEnd));
					}
				}
			}
			CreateLipAnimationKeys(lipAnimationList, audioData.Shapes);
		}

		private void CreateLipAnimationFromTextString(AudioFileInfo audioData, TimeSpan audioDuration)
		{
			var letterPathDictionary = GetLetterPathDictionary(CharacterVisemeList);
			// List<letter, duration>.
			var textListDuration = new List<(string, int)>();
			var textList = ConvertStringToList(audioData);
			double textDuration = 0;
			foreach (var item in textList)
			{
				// "ū" > "u".
				var letter = Filters.FoldToASCII(item);
				if (letterPathDictionary.ContainsKey(letter))
				{
					textListDuration.Add((letter, letterPathDictionary[letter].Item2));
					textDuration += letterPathDictionary[letter].Item2;
				}
				else
				{
					textListDuration.Add((letter, letterPathDictionary["*"].Item2));
					textDuration += letterPathDictionary["*"].Item2;
				}
			}
			// Create list (VisemeID, Text, Path, Duration).
			var lipAnimationList = new List<(string, Path, double)>();
			double timeEnd = 0;
			double adjustmentFactor = (audioDuration.TotalMilliseconds - 1000) / textDuration;
			foreach (var (letter, duration) in textListDuration)
			{
				if (letterPathDictionary.ContainsKey(letter))
				{
					timeEnd = timeEnd + duration * adjustmentFactor;
					lipAnimationList.Add((letter, letterPathDictionary[letter].Item1, timeEnd));
				}
			}
			CreateLipAnimationKeys(lipAnimationList, audioData.Shapes);
		}

		// "<speak version=\"1.0\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xml:lang=\"en-US\" xmlns=\"http://www.w3.org/2001/10/synthesis\">\r\n  <voice name=\"lt-LT-LeonasNeural\">\r\n <mstts:viseme type=\"FacialExpression\" />text</voice>\r\n</speak>"
		private string ExtractTextFromXmlString(string xmlString)
		{
			XDocument doc = XDocument.Parse(xmlString);
			XElement root = doc.Root;
			XElement voiceElement = root.Element("{http://www.w3.org/2001/10/synthesis}voice");
			if (voiceElement != null) { return voiceElement.Value; }
			else { return xmlString; }
		}

		private List<string> ConvertStringToList(AudioFileInfo audioData)
		{
			List<string> audioTextList = new List<string>();
			var audioText = ExtractTextFromXmlString(audioData.Text).ToLower();
			audioText = audioText + " ";
			for (int i = 0; i < audioText.Length; i++)
			{
				// Check for combination of two letters.
				if (i + 1 < audioText.Length && GetMultiples(CharacterVisemeList).Contains(audioText.Substring(i, 2)))
				{
					audioTextList.Add(audioText.Substring(i, 2));
					i++; // Skip the next character as it is already included in the combination.
				}
				// Check for space and ensure it's added only once for consecutive spaces.
				else if (audioText[i] == ' ')
				{
					// If the previous letter in the list isn't a space, add it.
					if (audioTextList.LastOrDefault() != " ") { audioTextList.Add(" "); }
				}
				else if (char.IsLetter(audioText[i]))
				{
					audioTextList.Add(audioText[i].ToString());
				}
				else if (audioText[i] == '.' ||
						 audioText[i] == ',' ||
						 audioText[i] == ':' ||
						 audioText[i] == ';' ||
						 audioText[i] == '!' ||
						 audioText[i] == '?')
				{
					audioTextList.Add(audioText[i].ToString());
				}
				// Other character types (digits, punctuation).
			}
			return audioTextList;
		}

		private void CreateLipAnimationKeys(List<(string, Path, double)> lipAnimationList, List<BlendShape> blendShapeList)
		{
			// Reset values.
			double timeNow = 0;
			double animationBar = 0;
			double animationBarStep = 1000 / Convert.ToDouble(lipAnimationList.Count());
			// VisemeID, Text, Path, Duration.
			foreach (var (text, path, timeEnd) in lipAnimationList)
			{
				var pathNowList = ExtractNumbersFromPathData(pathNow.Data);
				var pathEndList = ExtractNumbersFromPathData(path.Data);
				pathNow = path;
				animationBar = animationBar + animationBarStep;
				var duration = timeEnd - timeNow;
				// Add multiple KeyFrames.
				for (int i = 1; i < LipAnimationFrames + 1; i++)
				{
					var ms = TimeSpan.FromMilliseconds(timeNow + duration / LipAnimationFrames * i);
					// Create Path Data KeyFrames.
					var p = new List<double>();
					for (int item = 0; item < pathNowList.Count; item++) { p.Add((pathEndList[item] - pathNowList[item]) / LipAnimationFrames * i + pathNowList[item]); }
					animation_PTH.KeyFrames.Add(new DiscreteStringKeyFrame { KeyTime = ms, Value = Geometry.Parse($"M {p[0]},{p[1]} C {p[2]},{p[3]} {p[4]},{p[5]} {p[6]},{p[7]} {p[8]},{p[9]} {p[10]},{p[11]} {p[12]},{p[13]} Z").ToString() });
					// Create Letter Text KeyFrames.
					animation_TXT.KeyFrames.Add(new DiscreteStringKeyFrame(text, ms));
				}
				// Add 1 KeyFrame.
				animation_BAR.KeyFrames.Add(new DiscreteDoubleKeyFrame(Math.Round(animationBar), TimeSpan.FromMilliseconds(timeEnd)));
				timeNow = timeEnd;
			}

			// Create eye iris animations.
			//var scaleX = 10M;
			//var scaleY = 3M;

			//foreach (var item in blendShapeList)
			//{
			//	var startingFrameIndex = item.FrameIndex;
			//	var shapes = item.BlendShapes;
			//	for (int f = 0; f < shapes.Length; f++)
			//	{
			//		var frameIndex = startingFrameIndex + f;
			//		var frameOffset = (int)(frameIndex * 1000M / 60M);
			//		var frameShapes = shapes[f];

			//		//Convert BlendShapes to left and top Margins: 2 eyeLookDownLeft, 5 eyeLookUpLeft, 4 eyeLookOutLeft, 3 eyeLookInLeft.
			//		var eyeLY = Convert.ToInt32((frameShapes[2] - frameShapes[5]) * scaleY).ToString();
			//		var eyeLX = Convert.ToInt32((frameShapes[4] - frameShapes[3]) * scaleX).ToString();
			//		//Convert BlendShapes to left and top Margins: 9 eyeLookDownRight, 12 eyeLookUpRight, 11 eyeLookOutRight, 10 eyeLookInRight.
			//		var eyeRY = Convert.ToInt32((frameShapes[9] - frameShapes[12]) * scaleY).ToString();
			//		var eyeRX = Convert.ToInt32((frameShapes[11] - frameShapes[10]) * scaleX).ToString();

			//		var irisLMargins = eyeLX + "," + eyeLY + "," + eyeRX + "," + eyeRY;
			//		animation_EYE.KeyFrames.Add(new DiscreteStringKeyFrame(irisLMargins, TimeSpan.FromMilliseconds(frameOffset)));
			//	}
			//}
		}

		private List<double> ExtractNumbersFromPathData(Geometry pathData)
		{
			List<double> numbersList = new List<double>();
			// Regex matches positive/negative integers and floats.
			var matches = Regex.Matches(pathData.ToString(), @"-?\d+(?:\.\d+)?");

			foreach (Match match in matches)
			{
				if (double.TryParse(match.Value, out double result))
				{
					numbersList.Add(result);
				}
			}
			return numbersList;
		}

		private double PositionY(Geometry data, double position)
		{
			Point pointOnPath, tangentAtPoint;
			PathGeometry.CreateFromGeometry(data).GetPointAtFractionLength(position, out pointOnPath, out tangentAtPoint);
			return Convert.ToDouble(pointOnPath.Y);
		}

		private void StoryboardLips_CurrentTimeInvalidated(object sender, EventArgs e)
		{
			if (PathAnimationTextBlock.Text != null)
			{
				var data = Geometry.Parse(PathAnimationTextBlock.Text);
				MouthPath.Data = data;
				SetLipsMeshGeometry3DUsingPathData(data);
				//SetEmotions();
			}
		}

		//private void SetEmotions()
		//{
		//	var marginString = IrisAnimationTextBlock.Text;
		//	string[] parts = marginString.Split(',');
		//	if (parts.Length == 4)
		//	{
		//		try
		//		{
		//			// Create a new Thickness instance using the parsed integers.
		//			EyeIrisLImage.Margin = new Thickness(int.Parse(parts[0]), int.Parse(parts[1]), 0, 0);
		//			EyeIrisRImage.Margin = new Thickness(int.Parse(parts[2]), int.Parse(parts[3]), 0, 0);
		//		}
		//		catch (FormatException fe)
		//		{
		//			Console.WriteLine("An error occurred while parsing the string: " + fe.Message);
		//			// Handle the format exception (e.g., if the string does not contain valid integers).
		//		}
		//	}
		//}

		double scale3D = 2100;
		int maxPoints = 15;

		private void SetLipsMeshGeometry3DUsingPathData(Geometry data)
		{
			// Divide PathGeometry.
			// M 715,515 C 930,440 1170,440 1385,515 1170,720 930,720 715,515 Z
			// M 715,515 C 930,440 1170,440 1385,515 (-->)
			// M 1385,515 C 1170,720 930,720 715,515 (<--)
			// M 715,515 C 930,720 1170,720 1385,515 (reversed -->)
			PathGeometry geometry = PathGeometry.CreateFromGeometry(data);
			PathFigure figure = geometry.Figures.First();
			PolyBezierSegment segment = (PolyBezierSegment)figure.Segments[0].Clone();
			// Create two PathGeometries from one PathGeometry for more precise position and angle calculations later.
			var segment1 = new BezierSegment { Point1 = segment.Points[0], Point2 = segment.Points[1], Point3 = segment.Points[2] };
			var segment2 = new BezierSegment { Point1 = segment.Points[4], Point2 = segment.Points[3], Point3 = segment.Points[2] };
			var figure1 = new PathFigure { StartPoint = figure.StartPoint, Segments = { segment1 } };
			var figure2 = new PathFigure { StartPoint = figure.StartPoint, Segments = { segment2 } };
			var geometry1 = new PathGeometry { Figures = { figure1 } };
			var geometry2 = new PathGeometry { Figures = { figure2 } };

			// Bottom jaw (Path is 6 times bigger than Image). 200px is height of image. 2 - reduce max heigh range by half.
			var jawImageheigh = Math.Max(0, (PositionY(geometry, 0.75) - PositionY(MPath_0.Data, 0.75)) / 6) / 2 + 200;
			JawGrid.Height = jawImageheigh;
			AvatarMouth.Figures = geometry.Figures;
			LipTop.ImageSource = jawImageheigh > 200 ? bitmapImageLip1 : bitmapImageLip0;

			int p1 = LipGeometryDivisions + 1; // start point1 6
			int p2 = maxPoints - 1; // start point2 14
			double positionOnPath = 0;
			double positionOnPathStep = 1 / (double)LipGeometryDivisions;

			for (int i = 1; i < p1 + 1; i++)
			{
				foreach (PathGeometry g in new[] { geometry1, geometry2 })
				{
					double distance1 = g == geometry1 ? 200 : 10;
					double distance2 = g == geometry1 ? 35 : 310;
					// Get positionOnPath position and angle.
					g.GetPointAtFractionLength(positionOnPath, out var pathPoint, out var pathTangent);
					double angleDegrees = Math.Atan2(pathTangent.Y, pathTangent.X) * (180 / Math.PI);
					// Convert degrees to radians and calculate positions.
					double angleRadians = (angleDegrees + 270) * Math.PI / 180;
					double x1 = pathPoint.X + distance1 * Math.Cos(angleRadians);
					double y1 = pathPoint.Y + distance1 * Math.Sin(angleRadians);
					double x2 = pathPoint.X - distance2 * Math.Cos(angleRadians);
					double y2 = pathPoint.Y - distance2 * Math.Sin(angleRadians);
					// Convert Viewport values for Viewport3D.
					var X1 = x1 / scale3D;
					var X2 = x2 / scale3D;
					var Y1 = Math.Abs(y1 / scale3D - 1);
					var Y2 = Math.Abs(y2 / scale3D - 1);

					if (g == geometry1)
					{
						LipTopBackgroundMeshGeometry3D.Positions[i] = new Point3D(pathPoint.X / scale3D, Math.Abs(pathPoint.Y / scale3D - 1), 0);
						LipTopMeshGeometry3D.Positions[p2] = new Point3D(X1, Y1, 0);
						LipTopMeshGeometry3D.Positions[i] = new Point3D(X2, Y2, 0);
					}
					else
					{
						LipBottomBackgroundMeshGeometry3D.Positions[p2] = new Point3D(pathPoint.X / scale3D, Math.Abs(pathPoint.Y / scale3D - 1), 0);
						LipBottomMeshGeometry3D.Positions[p2] = new Point3D(X1, Y1, 0);
						LipBottomMeshGeometry3D.Positions[i] = new Point3D(X2, Y2, 0);
					}
					// Distances from position on Path. double meshLineLength = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
				}
				positionOnPath = positionOnPath + positionOnPathStep;
				p2 = p2 - 1;
			}
		}

		private void SetLipsMeshGeometry3D()
		{
			foreach (var g in new List<MeshGeometry3D> { LipTopBackgroundMeshGeometry3D, LipBottomBackgroundMeshGeometry3D, LipTopMeshGeometry3D, LipBottomMeshGeometry3D, })
			{
				g.Positions.Clear();
				g.TextureCoordinates.Clear();
				g.TriangleIndices.Clear();
			}

			// Create TriangleIndices.
			var triangleIndicesT = new Int32Collection();
			var triangleIndicesB = new Int32Collection();
			maxPoints = LipGeometryDivisions * 2 + 5; // 15

			// Start.
			// (/) 0, 1, 14, 14, 15, 0
			// (\) 0, 1, 15, 1, 14, 15	
			triangleIndicesT.Add(0);
			triangleIndicesT.Add(1);
			triangleIndicesT.Add(maxPoints - 1);
			triangleIndicesT.Add(maxPoints - 1);
			triangleIndicesT.Add(maxPoints);
			triangleIndicesT.Add(0);

			triangleIndicesB.Add(0);
			triangleIndicesB.Add(1);
			triangleIndicesB.Add(maxPoints);
			triangleIndicesB.Add(1);
			triangleIndicesB.Add(maxPoints - 1);
			triangleIndicesB.Add(maxPoints);

			// Middle.
			// 1 2 13 13 14 1
			// 2 3 12 12 13 2
			// 3 4 11 11 12 3
			// 4 5 10 10 11 4
			// 5 6  9  9 10 5
			var p1 = 1;
			var p2 = 2;
			var p3 = maxPoints - 2;
			var p4 = maxPoints - 1;
			for (int i = 1; i < LipGeometryDivisions + 1; i++)
			{
				// Top.
				triangleIndicesT.Add(p1);
				triangleIndicesT.Add(p2);
				triangleIndicesT.Add(p3);
				triangleIndicesT.Add(p3);
				triangleIndicesT.Add(p4);
				triangleIndicesT.Add(p1);
				// Bottom.
				triangleIndicesB.Add(p1);
				triangleIndicesB.Add(p2);
				triangleIndicesB.Add(p3);
				triangleIndicesB.Add(p3);
				triangleIndicesB.Add(p4);
				triangleIndicesB.Add(p1);
				p1 = p1 + 1;
				p2 = p2 + 1;
				p3 = p3 - 1;
				p4 = p4 - 1;
			}

			// End.
			// (\) 6, 7, 9, 7, 8, 9
			// (/) 6, 7, 8, 8, 9, 6
			triangleIndicesT.Add(LipGeometryDivisions + 1);
			triangleIndicesT.Add(LipGeometryDivisions + 2);
			triangleIndicesT.Add(LipGeometryDivisions + 4);
			triangleIndicesT.Add(LipGeometryDivisions + 2);
			triangleIndicesT.Add(LipGeometryDivisions + 3);
			triangleIndicesT.Add(LipGeometryDivisions + 4);

			triangleIndicesB.Add(LipGeometryDivisions + 1);
			triangleIndicesB.Add(LipGeometryDivisions + 2);
			triangleIndicesB.Add(LipGeometryDivisions + 3);
			triangleIndicesB.Add(LipGeometryDivisions + 3);
			triangleIndicesB.Add(LipGeometryDivisions + 4);
			triangleIndicesB.Add(LipGeometryDivisions + 1);

			LipTopBackgroundMeshGeometry3D.TriangleIndices = triangleIndicesT;
			LipTopMeshGeometry3D.TriangleIndices = triangleIndicesT;
			LipBottomBackgroundMeshGeometry3D.TriangleIndices = triangleIndicesB;
			LipBottomMeshGeometry3D.TriangleIndices = triangleIndicesB;

			// Create Positions and TextureCoordinates.
			var positions = new List<(double, int)>();
			var textureCoordinates = new PointCollection();

			// X.
			var x = new[] { 456, 720, 1380, 1644 };
			// Background.
			var yBT = new[] { 510, 270 };
			var yBB = new[] { 870, 510 };
			// Lips.
			var yLT = new[] { 545, 310 };
			var yLB = new[] { 820, 500 };
			// Texture.
			var yTX = new[] { 1, 0 };
			double textureWidth = x[3] - x[0]; // 1188

			var xStep = (x[2] - x[1]) / LipGeometryDivisions;
			var xPosition = x[1];
			// Forward.
			positions.Add((x[0], 0));
			positions.Add((x[1], 0));
			for (int i = 1; i < LipGeometryDivisions + 1; i++)
			{
				xPosition = xPosition + xStep;
				positions.Add((xPosition, 0));
			}
			positions.Add((x[3], 0));
			// Reverse.
			positions.Add((x[3], 1));
			positions.Add((x[2], 1));
			for (int i = 1; i < LipGeometryDivisions + 1; i++)
			{
				xPosition = xPosition - xStep;
				positions.Add((xPosition, 1));
			}
			positions.Add((x[0], 1));

			foreach (var (X, Y) in positions)
			{
				// Set scaled Positions.
				LipTopBackgroundMeshGeometry3D.Positions.Add(new Point3D(X / scale3D, Math.Abs(yBT[Y] / scale3D - 1), 0));
				LipBottomBackgroundMeshGeometry3D.Positions.Add(new Point3D(X / scale3D, Math.Abs(yBB[Y] / scale3D - 1), 0));
				LipTopMeshGeometry3D.Positions.Add(new Point3D(X / scale3D, Math.Abs(yLT[Y] / scale3D - 1), 0));
				LipBottomMeshGeometry3D.Positions.Add(new Point3D(X / scale3D, Math.Abs(yLB[Y] / scale3D - 1), 0));
				// Create TextureCoordinates.
				textureCoordinates.Add(new Point((X - x[0]) / textureWidth, yTX[Y]));
			}

			foreach (var g in new List<MeshGeometry3D> { LipTopBackgroundMeshGeometry3D, LipBottomBackgroundMeshGeometry3D, LipTopMeshGeometry3D, LipBottomMeshGeometry3D, })
			{
				g.TextureCoordinates = textureCoordinates;
			}
		}

		private void CreateVisemePathDictionary()
		{
			visemePathDictionary = new Dictionary<int, Path>
			{
				{0,MPath_0},
				{1,MPath_1},
				{2,MPath_2},
				{3,MPath_3},
				{4,MPath_4},
				{5,MPath_5},
				{6,MPath_6},
				{7,MPath_7},
				{8,MPath_8},
				{9,MPath_9},
				{10,MPath_10},
				{11,MPath_11},
				{12,MPath_12},
				{13,MPath_13},
				{14,MPath_14},
				{15,MPath_15},
				{16,MPath_16},
				{17,MPath_17},
				{18,MPath_18},
				{19,MPath_19},
				{20,MPath_20},
				{21,MPath_21},
			};
		}

		List<(string, int, int)> CharacterVisemeList = new List<(string, int, int)>
		{
				// Multiple.
				{("ch", 12, 400)},
				// Single / Silence.
				{(" ", 0, 600)},
				{(".", 0, 1000)},
				{(",", 0, 600)},
				{(":", 0, 600)},
				{(";", 0, 600)},
				{("?", 0, 600)},
				{("!", 0, 600)},
				// Single.
				{("*", 6, 600)},
				{("a", 1, 800)},
				{("e", 1, 750)},
				{("u", 1, 750)},
				{("z", 5, 550)},
				{("i", 6, 750)},
				{("y", 6, 750)},
				{("w", 7, 700)},
				{("o", 8, 800)},
				{("h", 12, 400)},
				{("r", 13, 600)},
				{("l", 14, 600)},
				{("s", 15, 500)},
				{("x", 15, 500)},
				{("j", 16, 500)},
				{("f", 18, 400)},
				{("v", 18, 550)},
				{("d", 19, 500)},
				{("n", 19, 500)},
				{("t", 19, 400)},
				{("c", 20, 400)},
				{("g", 20, 500)},
				{("k", 20, 400)},
				{("q", 20, 600)},
				{("b", 21, 500)},
				{("m", 21, 600)},
				{("p", 21, 400)},
		};

		//new List<(string, int, int)> VisemesLT = new List<(string, int, int)>
		//{
		//		// Multiple.
		//		{("ch", 12, 400)},
		//		// Single.
		//		{(" ", 0, 600)},
		//		{(".", 0, 900)},
		//		{(",", 0, 600)},
		//		{(":", 0, 600)},
		//		{(";", 0, 600)},
		//		{("?", 0, 600)},
		//		{("!", 0, 600)},
		//		{("a", 1, 800)},
		//		{("ą", 1, 800)},
		//		{("e", 1, 750)},
		//		{("ę", 1, 750)},
		//		{("ė", 1, 750)},
		//		{("u", 1, 750)},
		//		{("ų", 1, 750)},
		//		{("ū", 1, 750)},
		//		{("*", 6, 600)},
		//		{("y", 6, 750)},
		//		{("į", 6, 750)},
		//		{("i", 6, 750)},
		//		{("w", 7, 700)},
		//		{("o", 8, 800)},
		//		{("h", 12, 400)},
		//		{("r", 13, 600)},
		//		{("l", 14, 600)},
		//		{("x", 15, 500)},
		//		{("s", 15, 500)},
		//		{("š", 15, 500)},
		//		{("z", 15, 550)},
		//		{("ž", 15, 550)},
		//		{("j", 16, 500)},
		//		{("f", 18, 400)},
		//		{("v", 18, 550)},
		//		{("d", 19, 500)},
		//		{("t", 19, 400)},
		//		{("n", 19, 500)},
		//		{("c", 20, 400)},
		//		{("č", 20, 400)},
		//		{("k", 20, 400)},
		//		{("q", 20, 600)},
		//		{("g", 20, 500)},
		//		{("p", 21, 400) },
		//		{("b", 21, 500) },
		//		{("m", 21, 600) },
		//};

		// List<(string, int, int)> VisemesEN = new List<(string, int, int)>
		//{
		//		// Multiple.
		//		{("aw", 3, 800)}, // ɔ
		//		{("oo", 4, 850)}, // ʊ
		//		{("er", 5, 800)}, // ɝ
		//		{("ih", 6, 750)}, // ɪ
		//		{("ou", 9, 900)}, // aʊ
		//		{("oy", 10, 850)}, // ɔɪ
		//		{("ai", 11, 900)}, // aɪ
		//		{("sh", 16, 500)}, // ʃ
		//		{("ch", 16, 500)}, // tʃ
		//		{("zh", 16, 550)}, // ʒ
		//		{("th", 17, 500)}, // ð // {"th",MPath_19}, // θ
		//		{("ng", 20, 650)}, // ŋ
		//		// Single.
		//		{(" ", 0, 600)}, // Silence.
		//		{(".", 0, 1000)},
		//		{(",", 0, 600)},
		//		{(":", 0, 600)},
		//		{(";", 0, 600)},
		//		{("?", 0, 600)},
		//		{("!", 0, 600)},
		//		{("a", 1, 800)}, // æ // {"a",MPath_2}, // ɑ
		//		{("e", 1, 750)}, // ə //{"e",MPath_4}, // ɛ
		//		{("u", 1, 750)}, // ʌ
		//		{("z", 5, 550)} , // z
		//		{("*", 6, 600)}, // *
		//		{("y", 6, 750)}, // j
		//		{("i", 6, 750)}, // i
		//		{("w", 7, 700)}, // w // {"u"MPath_7}, // u
		//		{("o", 8, 800)}, // o // {"o",MPath_3}, // ɔ
		//		{("h", 12, 400)}, // h
		//		{("r", 13, 600)}, // ɹ
		//		{("l", 14, 600)}, // l
		//		{("x", 15, 500)}, // x
		//		{("s", 15, 500)}, // s
		//		{("j", 16, 500)}, // dʒ
		//		{("f", 18, 400)}, // f
		//		{("v", 18, 550)}, // v
		//		{("d", 19, 500)}, // d
		//		{("t", 19, 400)}, // t
		//		{("n", 19, 500)}, // n
		//		{("c", 20, 400)}, // c
		//		{("k", 20, 400)}, // k
		//		{("q", 20, 600)}, // q
		//		{("g", 20, 500)}, // g
		//		{("p", 21, 400)}, // p
		//		{("b", 21, 500)}, // b
		//		{("m", 21, 600)}, // m
		//};

		private Dictionary<string, Tuple<Path, int>> GetLetterPathDictionary(List<(string, int, int)> visemes)
		{
			var dictionary = new Dictionary<string, Tuple<Path, int>>();
			foreach (var (letter, viseme, duration) in visemes)
			{
				dictionary.Add(letter, new Tuple<Path, int>(visemePathDictionary[viseme], duration));
			}
			return dictionary;
		}

		private HashSet<string> GetMultiples(List<(string, int, int)> visemes)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (var (letter, viseme, duration) in visemes)
			{
				if (letter.Length > 1) { hashSet.Add(letter); }
			}
			return hashSet;
		}

		// Create BACKGROUND (CAMERA, AURA, SPARK) animations.
		public void CreateBackgroundAnimations()
		{
			// Un​interruptible animations.
			Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
			{
				// Time settings.
				var beginTimeMin = 0;
				var beginTimeMax = 60000;
				var duration = 10000;
				// Create storyboardBackground animations.
				CreateCameraAnimation(beginTimeMin, duration);
				CreateAuraAnimation(beginTimeMin, duration);
				CreateSparkAnimation(beginTimeMin, beginTimeMax, duration);
				// Begin animation.
				storyboardBackground.SpeedRatio = 0.3;
				storyboardBackground.Begin();
				storyboardBackground.Seek(TimeSpan.FromMilliseconds(beginTimeMax));
			}));
		}

		// CAMERA animation.
		private void CreateCameraAnimation(int beginTime, int duration)
		{
			var animation = new MatrixAnimationUsingPath
			{
				RepeatBehavior = RepeatBehavior.Forever,
				BeginTime = TimeSpan.FromMilliseconds(beginTime),
				Duration = TimeSpan.FromMilliseconds(duration),
				PathGeometry = PathGeometry.CreateFromGeometry(CameraPath.Data),
			};
			Storyboard.SetTarget(animation, CameraAnimationViewbox);
			Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(MatrixTransform.Matrix)"));
			storyboardBackground.Children.Add(animation);
		}

		// AURA animation.
		private void CreateAuraAnimation(int start, int duration)
		{
			// Create Grid for Ellipses.
			var grid = new Grid
			{
				Height = 1160,
				Width = 1080,
				ClipToBounds = true,
				VerticalAlignment = VerticalAlignment.Top,
			};
			AuraCanvas.Children.Add(grid);

			// Create gradient for Ellipses.
			Color c1 = (Color)ColorConverter.ConvertFromString("#00000000");
			Color c2 = (Color)ColorConverter.ConvertFromString("#aa77ccff");
			RadialGradientBrush AuraGradient = new RadialGradientBrush
			{
				GradientStops = new GradientStopCollection
				{
					new GradientStop(c1, 0.86),
					new GradientStop(c2, 0.89),
					new GradientStop(c2, 0.90),
					new GradientStop(c1, 0.93),
				}
			};

			// Create 3 Ellipses and animations for them.
			foreach (int ms in new[] { 0, 3333, 6666 })
			{
				// Create Ellipse.
				var ellipse = new Ellipse
				{
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Fill = AuraGradient,
				};
				grid.Children.Add(ellipse);

				// Create Ellipse size animation.
				foreach (string property in new[] { "Width", "Height" })
				{
					var animation = new DoubleAnimationUsingKeyFrames
					{
						RepeatBehavior = RepeatBehavior.Forever,
						BeginTime = TimeSpan.FromMilliseconds(ms),
					};
					animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(200, TimeSpan.FromMilliseconds(start)));
					animation.KeyFrames.Add(new LinearDoubleKeyFrame(1600, TimeSpan.FromMilliseconds(duration)));
					Storyboard.SetTarget(animation, ellipse);
					Storyboard.SetTargetProperty(animation, new PropertyPath(property));
					storyboardBackground.Children.Add(animation);
				}
			}
		}

		// GLOW animation.
		private void CreateGlowAnimation()
		{
			animation_GLW.Completed += storyboardGlowAnimationContinue;
			animation_GLW.Duration = TimeSpan.FromMilliseconds(2000);
			animation_GLW.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.Zero));
			animation_GLW.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(1000)));
			animation_GLW.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(2000)));
			Storyboard.SetTarget(animation_GLW, GlowWhiteImage);
			Storyboard.SetTargetProperty(animation_GLW, new PropertyPath("Opacity"));
			storyboardGlow.Children.Add(animation_GLW);
		}

		// SPARK animation.
		private void CreateSparkAnimation(int startMin, int startMax, int duration)
		{
			var sizeMax = 100;
			var sizeMinRandom = 80;
			var sizeMaxRandom = 100;
			var random = new Random();

			foreach (Canvas canvas in new[] { SparksYellowCanvas, SparksBlueCanvas, SparksBrownCanvas })
			{
				foreach (Path path in canvas.Children)
				{
					var startRandom = TimeSpan.FromMilliseconds(random.Next(startMin, startMax));

					// Create Grid for Image ("spark").
					var grid = new Grid
					{
						Height = sizeMax,
						Width = sizeMax,
						Margin = new Thickness(-sizeMax / 2, -sizeMax / 2, 0, 0),
					};
					SparkCanvas.Children.Add(grid);

					// Create Grid moving along the path animation.
					if (canvas == SparksBrownCanvas)
					{
						var pathNumbers = ExtractNumbersFromPathData(path.Data);
						Canvas.SetLeft(grid, pathNumbers[0]);
						Canvas.SetTop(grid, pathNumbers[1]);
					}
					else
					{
						// Create Grid moving along the path animation.
						var animationPath = new MatrixAnimationUsingPath
						{
							RepeatBehavior = RepeatBehavior.Forever,
							BeginTime = startRandom,
							Duration = TimeSpan.FromMilliseconds(duration),
							PathGeometry = PathGeometry.CreateFromGeometry(path.Data),
						};
						Storyboard.SetTarget(animationPath, grid);
						Storyboard.SetTargetProperty(animationPath, new PropertyPath("(UIElement.RenderTransform).(MatrixTransform.Matrix)"));
						storyboardBackground.Children.Add(animationPath);
					};

					// Create Image.
					var image = new Image
					{
						Height = 0,
						Width = 0,
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						Source = (canvas == SparksYellowCanvas) ? bitmapImageYellow : (canvas == SparksBlueCanvas) ? bitmapImageBlue : bitmapImageBrown,
					};
					grid.Children.Add(image);

					// Create Image size animation.
					var sizeMaxR = random.Next(sizeMax * sizeMinRandom, sizeMax * sizeMaxRandom) / sizeMax;
					var durationB = 800;

					foreach (string property in new[] { "Width", "Height" })
					{
						var animationSize = new DoubleAnimationUsingKeyFrames
						{
							RepeatBehavior = RepeatBehavior.Forever,
							BeginTime = startRandom,
						};

						if (canvas == SparksYellowCanvas)
						{
							animationSize.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.Zero));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration / 5)));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration)));
						}
						else if (canvas == SparksBlueCanvas)
						{
							animationSize.KeyFrames.Add(new DiscreteDoubleKeyFrame(sizeMaxR, TimeSpan.Zero));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(duration / 2)));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(duration)));
						}
						else if (canvas == SparksBrownCanvas)
						{
							animationSize.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.Zero));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(sizeMaxR, TimeSpan.FromMilliseconds(durationB / 4)));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(durationB / 2)));
							animationSize.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(durationB)));
						}
						Storyboard.SetTarget(animationSize, image);
						Storyboard.SetTargetProperty(animationSize, new PropertyPath(property));
						storyboardBackground.Children.Add(animationSize);
					}
				}
			}
		}
	}
}
