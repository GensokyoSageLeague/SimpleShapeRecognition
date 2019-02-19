﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotSpatial.Data;
using DotSpatial.Modeling.Forms;
using DotSpatial.Topology;
using DotSpatial.Topology.Algorithm;
using System.Collections;

namespace MyGIS.Tools.Specific {
	public class LiCircle : Tool {
		private Parameter[] _inputParam;
		private Parameter[] _outputParam;

		public override Parameter[] InputParameters {
			get {
				return _inputParam;
			}
		}

		public override Parameter[] OutputParameters {
			get {
				return _outputParam;
			}
		}

		public LiCircle() {
			this.Name = TextStrings.LiCircle;
			this.Category = TextStrings.Category;
			this.Description = TextStrings.LiCircleDescription;
			this.ToolTip = TextStrings.LICircleTip;
		}

		public override void Initialize() {
			_inputParam = new Parameter[4];
			_inputParam[0] = new LineFeatureSetParam(TextStrings.InputFeatureSet);
			_inputParam[1] = new DoubleParam(TextStrings.LiLinePara1, 60.0);
			_inputParam[2] = new DoubleParam(TextStrings.LiLinePara2, 3.0);
			_inputParam[3] = new DoubleParam(TextStrings.LiLinePara3, 5.0);
			_outputParam = new Parameter[1];
			_outputParam[0] = new PolygonFeatureSetParam(TextStrings.OutputFeatureSet);
		}

		struct FeatureUnion {
			internal IFeature OriginalLine;
			internal IFeature LinkedPolygon;
			internal IFeature EnvelopedRect;
			internal IFeature CentroidOfPolygon;
			internal bool Visited;
		}

		public override bool Execute(ICancelProgressHandler cancelProgressHandler) {
			IFeatureSet input = _inputParam[0].Value as IFeatureSet;
			IFeatureSet output = _outputParam[0].Value as IFeatureSet;
			DoubleParam para;
			para = _inputParam[1] as DoubleParam;
			double paraSearchRadius = para != null ? para.Value : 60;
			para = _inputParam[2] as DoubleParam;
			double paraSearchCount = para != null ? para.Value : 3;
			para = _inputParam[3] as DoubleParam;
			double paraToleranceOfConvexHull = para != null ? para.Value : 5;

			List<FeatureUnion> myunion = new List<FeatureUnion>();

			for (int i = 0; i < input.Features.Count; i++) {
				IFeature  feature = input.Features[i];
				IEnvelope envelop = input.Features[i].Envelope;

				if (feature.BasicGeometry.Coordinates[feature.BasicGeometry.Coordinates.Count - 1]
					!=
					feature.BasicGeometry.Coordinates[0])
				{
					continue;
				}

				var c1 = envelop.TopLeft();
				var c2 = envelop.BottomRight();
				var lp = new Polygon(feature.BasicGeometry.Coordinates);
				var lpf = new Feature(lp);
				var er = new Polygon(
					new Coordinate[] {
						new Coordinate(c1.X, c1.Y),
						new Coordinate(c1.X, c2.Y),
						new Coordinate(c2.X, c2.Y),
						new Coordinate(c2.X, c1.Y),
						new Coordinate(c1.X, c1.Y)
				});
				var erf = new Feature(er);
				var cop = lpf.Centroid();

				if (double.IsNaN(cop.Coordinates[0].X) ||
					double.IsNaN(cop.Coordinates[0].Y)) 
				{
					continue;
				}

				if (Math.Abs(lpf.ConvexHull().Area() - lpf.Area()) / lpf.Area() > paraToleranceOfConvexHull / 100) {
					continue;
				}

				myunion.Add(new FeatureUnion {
					OriginalLine = input.Features[i],
					LinkedPolygon = lpf,
					EnvelopedRect = erf,
					CentroidOfPolygon = cop,
					Visited = false
				});

				output.Features.Add(myunion[myunion.Count - 1].CentroidOfPolygon.Buffer(paraSearchRadius));

				cancelProgressHandler.Progress(
					string.Empty,
					Convert.ToInt32((Convert.ToDouble(i) / Convert.ToDouble(input.Features.Count)) * 100),
					string.Empty
					//(myunion[i].LinkedPolygon.Area() / myunion[i].EnvelopedRect.Area()).ToString()
				);
			}

			try {
				for (int i = 0; i < myunion.Count; i++) {
					Coordinate c = myunion[i].CentroidOfPolygon.BasicGeometry.Coordinates[0];
					int covers = 0;
					for (int j = 0; j < i; j++) {
						if (myunion[i].CentroidOfPolygon.Buffer(paraSearchRadius).Covers(myunion[j].CentroidOfPolygon)) {
							covers++;
						}
					}
					for (int j = i + 1; j < myunion.Count; j++) {
						if (myunion[i].CentroidOfPolygon.Buffer(paraSearchRadius).Covers(myunion[j].CentroidOfPolygon)) {
							covers++;
						}
					}
					if (covers >= paraSearchCount) {
						//output.Features.Add(myunion[i].CentroidOfPolygon.Buffer(tolerance));
					}

					cancelProgressHandler.Progress(
						string.Empty,
						Convert.ToInt32((Convert.ToDouble(i) / Convert.ToDouble(input.Features.Count)) * 100),
						string.Empty
);
				}
			}
			catch (Exception) {
			}

			// 测试
// 			for (int i = 0; i < myunion.Count; i++) {
// 				output.Features.Add(myunion[i].CentroidOfPolygon.Buffer(tolerance));
// 				output.Features.Add(myunion[i].EnvelopedRect);
// 				cancelProgressHandler.Progress(
// 					string.Empty,
// 					Convert.ToInt32((Convert.ToDouble(i) / Convert.ToDouble(input.Features.Count)) * 100),
// 					string.Empty
// 				);
// 			}

			output.Save();
			return true;
		}
	}
}