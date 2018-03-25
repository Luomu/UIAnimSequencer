using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

[ExecuteInEditMode]
public class UIAnimSequence : MonoBehaviour
{
	public enum Direction
	{
		From,
		To
	}

	public enum TravelDirection
	{
		Left,
		Right,
		Up,
		Down
	}

	public enum ActionType
	{
		//move local xyz (not used atm, use Fly instead)
		Move,
		//Spin the object (z rotation) from/to specified angle
		RotateZ,
		Scale,
		//Move in from/out to specified direction & distance
		Fly,
		//Modify canvas group alpha or image color alpha
		Fade,
		//Set an object active
		Show,
		//Set an object inactive
		Hide,
		//Call an unityevent
		Event,
		//Shake the object (move locally using perlin noise)
		Shake
	}

	[Serializable]
	public class SequenceAction
	{
		public ActionType Type;
		public LeanTweenType Easing;
		public Vector3 Vector;
		public float Float;
		public Direction Direction;
		public TravelDirection TravelDirection;
		public UnityEvent Event;
		public AnimationCurve Curve;

		[NonSerialized]
		public Vector3 OrigVector;
		[NonSerialized]
		public float OrigFloat;
		[NonSerialized]
		public Vector3 LocalPosAtStart;
	}

	[Serializable]
	public class SequenceStep
	{
		public bool LinkToPrevious;

		public GameObject Target;
		public float Delay;
		public float Duration;

		//This is a list so it's possible to expand to
		//multi-action steps if necessary
		public List<SequenceAction> Actions;
	}

	[SerializeField]
	List<SequenceStep> Elements = new List<SequenceStep>();
	[SerializeField]
	bool m_playOnAwake = true;
	//In case of "from" animations, sets objects to their first-frame state on awake,
	//in case Play is called later. Usually there is no reason to turn this off.
	[SerializeField]
	bool m_setToInitialStateOnAwake = true;

	bool m_animating;
	public bool Animating { get { return m_animating; } private set { m_animating = value; } }
	private bool HasDefaultState { get; set; }
	//If tweens are created on awake, but not started, we need to toggle them later
	List<LTDescr> m_createdTweens = new List<LTDescr>();

	public void EditorPreview()
	{
		LeanTween.init();
		RunSequence();
	}

	void OnCompleteTheWholeThing()
	{
		Animating = false;

		if (!Application.isPlaying)
			LeanTween.reset();
	}

	public void RunSequence(bool dontStartYet = false)
	{
		if (Animating)
			return;

		if (dontStartYet)
		{
			m_createdTweens = new List<LTDescr>();
		}
		else if (HasDefaultState)
		{
			foreach (var tween in m_createdTweens)
			{
				tween.toggle = true;
			}
			Animating = true;
			return;
		}

		float totalDelay = 0f;
		float delayOfPrevSequence = 0f;

		bool inEditor = !Application.isPlaying;

		//seems to result in smoother previews
		if (inEditor)
			totalDelay = 1.5f;

		//Record default state before any modifications
		foreach (var step in Elements)
		{
			if (step.Target == null)
			{
				Debug.LogError("Step has no target object");
				return;
			}

			var xform = step.Target.GetComponent<RectTransform>();
			if (xform == null)
			{
				Debug.LogError(step.Target.name + " has no RectTransform");
				return;
			}

			foreach (var action in step.Actions)
			{
				SaveActionOriginalState(step, action, xform);
			}
		}
		HasDefaultState = true;
		Animating = true;

		//Go through all sequence steps
		foreach (var step in Elements)
		{
			if (step.Target == null)
				continue;

			RectTransform xform = step.Target.GetComponent<RectTransform>();

			float delayForThisSequence;
			if (step.LinkToPrevious)
				delayForThisSequence = step.Delay + delayOfPrevSequence;
			else
			{
				delayForThisSequence = step.Delay + totalDelay;
				delayOfPrevSequence = delayForThisSequence;
			}

			//Go through actions and add tweens
			foreach (var action in step.Actions)
			{
				LTDescr descr;
				switch (action.Type)
				{
					case ActionType.Move:
						{
							var origpos = xform.anchoredPosition3D;
							xform.anchoredPosition3D = xform.anchoredPosition3D + action.Vector;
							descr = LeanTween.move(xform, origpos, step.Duration);
							break;
						}
					case ActionType.RotateZ:
						{
							if (action.Direction == Direction.From)
							{
								Vector3 origRot = xform.localRotation.eulerAngles;
								xform.localEulerAngles = xform.localEulerAngles.WithZ(xform.localEulerAngles.z - action.Float);
								descr = LeanTween.rotateAroundLocal(step.Target, new Vector3(0, 0, 1), action.Float, step.Duration);
							}
							else
							{
								descr = LeanTween.rotateAroundLocal(step.Target, new Vector3(0, 0, 1), action.Float, step.Duration);
							}
						}
						break;
					case ActionType.Scale:
						{
							if (action.Direction == Direction.From)
							{
								Vector3 tgtScale = xform.localScale;
								xform.localScale = action.Vector;
								descr = LeanTween.scale(step.Target, tgtScale, step.Duration);
							}
							else
							{
								descr = LeanTween.scale(step.Target, action.Vector, step.Duration);
							}
							break;
						}
					case ActionType.Fly:
						{
							Vector3 startPos = xform.anchoredPosition3D;
							float to = action.Float;

							switch (action.TravelDirection)
							{
								case TravelDirection.Left:
									if (action.Direction == Direction.From)
									{
										to = xform.anchoredPosition3D.x;
										startPos = new Vector3(startPos.x - action.Float, startPos.y, startPos.z);
										xform.anchoredPosition3D = startPos;
										descr = LeanTween.moveX(xform, to, step.Duration);
									}
									else
									{
										//passing correct to value to moveX is critical even if SetupMoveTo overrides it.
										//Leantween does a diff calculation with the to value before calling onStart.
										descr = LeanTween.moveX(xform, -action.Float, step.Duration);
										SetupMoveTo(descr, new Vector2(-action.Float, 0f));
									}

									break;
								case TravelDirection.Right:
									if (action.Direction == Direction.From)
									{
										to = xform.anchoredPosition3D.x;
										startPos = new Vector3(startPos.x + action.Float, startPos.y, startPos.z);
										xform.anchoredPosition3D = startPos;
										descr = LeanTween.moveX(xform, to, step.Duration);
									}
									else
									{
										descr = LeanTween.moveX(xform, to, step.Duration);
										SetupMoveTo(descr, new Vector2(action.Float, 0f));
									}

									break;
								case TravelDirection.Up:
									if (action.Direction == Direction.From)
									{
										to = xform.anchoredPosition3D.y;
										startPos = new Vector3(startPos.x, startPos.y + action.Float, startPos.z);
										xform.anchoredPosition3D = startPos;
										descr = LeanTween.moveY(xform, to, step.Duration);
									}
									else
									{
										descr = LeanTween.moveY(xform, to, step.Duration);
										SetupMoveTo(descr, new Vector2(0, action.Float));
									}

									break;
								default:
								case TravelDirection.Down:
									if (action.Direction == Direction.From)
									{
										to = xform.anchoredPosition3D.y;
										startPos = new Vector3(startPos.x, startPos.y - action.Float, startPos.z);
										xform.anchoredPosition3D = startPos;
										descr = LeanTween.moveY(xform, to, step.Duration);
									}
									else
									{
										descr = LeanTween.moveY(xform, -action.Float, step.Duration);
										SetupMoveTo(descr, new Vector2(0, -action.Float));
									}

									break;
							}
							break;
						}
					case ActionType.Fade:
						var cg = step.Target.GetComponent<CanvasGroup>();
						var graphic = step.Target.GetComponent<Graphic>();
						if (cg != null)
						{
							float tgtval;
							if (action.Direction == Direction.From)
							{
								float srcval = action.Float;
								tgtval = cg.alpha;
								cg.alpha = srcval;
							}
							else
							{
								tgtval = action.Float;
							}

							descr = LeanTween.alphaCanvas(cg, tgtval, step.Duration);
						}
						else //if (graphic != null)
						{
							float tgtval;
							if (action.Direction == Direction.From)
							{
								float srcval = action.Float;
								tgtval = graphic.color.a;
								graphic.color = graphic.color.WithA(srcval);
							}
							else
							{
								tgtval = action.Float;
							}

							descr = LeanTween.alpha(xform, tgtval, step.Duration);
						}
						break;
					case ActionType.Show:
						{
							descr = LeanTween.delayedCall(1f, () => step.Target.SetActive(true));
							descr.time = step.Duration; //todo check
							break;
						}
					case ActionType.Hide:
						{
							descr = LeanTween.delayedCall(1f, () => step.Target.SetActive(false));
							descr.time = step.Duration; //todo check
							break;
						}
					case ActionType.Event:
						{
							descr = LeanTween.delayedCall(1f, action.Event.Invoke);
							descr.time = step.Duration; //todo check
							break;
						}
					case ActionType.Shake:
						{
							Vector3 magnitude = action.Vector;
							float frequency = action.Float;
							descr = LeanTween.value(1, 0f, step.Duration)
								.setOnStart(() =>
								{
									action.LocalPosAtStart = xform.anchoredPosition3D;
								})
								.setOnUpdate((float v) =>
								{
									Vector3 origPos = action.LocalPosAtStart;
									float decay = 1f;//1f - v;
									float seed = Time.time * frequency;
									float p = Mathf.PerlinNoise(seed, 0f) - 0.5f;
									float py = Mathf.PerlinNoise(0f, seed) - 0.5f;
									var shakePosition = new Vector3(
										p * magnitude.x * decay,
										py * magnitude.y * decay,
										0f);
									xform.anchoredPosition3D = Vector3.LerpUnclamped(origPos, origPos + shakePosition, v);
								}).setOnComplete(() =>
								{
									xform.anchoredPosition3D = action.LocalPosAtStart;
								});
							break;
						}
					default:
						throw new NotImplementedException();
				}

				descr.setDelay(delayForThisSequence);
				if (action.Easing == LeanTweenType.animationCurve)
					descr.setEase(action.Curve);
				else
					descr.setEase(action.Easing);

				totalDelay = Math.Max(totalDelay, descr.delay + descr.time);

				//must for editor preview
				if (inEditor)
				{
					//descr.setIgnoreTimeScale(true);
					descr.setUseEstimatedTime(true);
					descr.setUseFrames(true);
				}

				if (dontStartYet)
				{
					descr.toggle = false;
					m_createdTweens.Add(descr);
					Animating = false;
				}
			}
		}
		//add one more call to trigger the end
		//Debug.Log("oncomplete will fire at " + totalDelay);
		LeanTween.delayedCall(totalDelay, OnCompleteTheWholeThing);
	}

	public void Play()
	{
		RunSequence();
	}

	private void Awake()
	{
		if (Application.isPlaying)
		{
			if (m_playOnAwake)
				RunSequence();
			else if (m_setToInitialStateOnAwake)
				RunSequence(dontStartYet: true);
		}
	}

	public void EditorForceUpdate()
	{
		if (Animating)
			LeanTween.update();
	}

	public void ResetToPreActionState()
	{
		Animating = false;
		if (HasDefaultState)
		{
			foreach (var step in Elements)
				foreach (var action in step.Actions)
					ResetActionToInitialState(step, action);

			HasDefaultState = false;
		}
	}

	private void SaveActionOriginalState(SequenceStep step, SequenceAction action, RectTransform xform)
	{
		switch (action.Type)
		{
			case ActionType.Move:
			case ActionType.Fly:
			case ActionType.Shake:
				action.OrigVector = xform.anchoredPosition3D;
				break;
			case ActionType.RotateZ:
				action.OrigVector = xform.rotation.eulerAngles;
				break;
			case ActionType.Scale:
				action.OrigVector = xform.localScale;
				break;
			case ActionType.Fade:
				{
					var cg = step.Target.GetComponent<CanvasGroup>();
					var graphic = step.Target.GetComponent<Graphic>();
					if (cg != null)
					{
						action.OrigFloat = cg.alpha;
					}
					else if (graphic != null)
					{
						action.OrigFloat = graphic.color.a;
					}
					break;
				}
			case ActionType.Hide:
			case ActionType.Show:
				action.OrigFloat = step.Target.activeSelf ? 1 : 0;
				break;
			default:
				break;
		}
	}

	private void ResetActionToInitialState(SequenceStep step, SequenceAction action)
	{
		var xform = step.Target.GetComponent<Transform>();
		var rectxform = step.Target.GetComponent<RectTransform>();
		switch (action.Type)
		{
			case ActionType.Move:
			case ActionType.Fly:
			case ActionType.Shake:
				rectxform.anchoredPosition3D = action.OrigVector;
				break;
			case ActionType.RotateZ:
				xform.rotation = Quaternion.Euler(action.OrigVector);
				break;
			case ActionType.Scale:
				xform.localScale = action.OrigVector;
				break;
			case ActionType.Fade:
				var cg = step.Target.GetComponent<CanvasGroup>();
				var graphic = step.Target.GetComponent<Graphic>();
				if (cg != null)
				{
					cg.alpha = action.OrigFloat;
				}
				else if (graphic != null)
				{
					graphic.color = graphic.color.WithA(action.OrigFloat);
				}
				break;
			case ActionType.Show:
			case ActionType.Hide:
				step.Target.SetActive(action.OrigFloat > 0f);
				break;
		}
	}

	// To make cumulative move actions work, the To value needs to adjusted
	// when the animation starts. Leantween already uses same approach to fetch the From value.
	private void SetupMoveTo(LTDescr descr, Vector2 amount)
	{
		descr.setOnStart(() =>
		{
			descr.to = descr.rectTransform.anchoredPosition3D + new Vector3(amount.x, amount.y, 0f);
		});
	}
}

public static class UIAnimUtils
{
	public static Color WithA(this Color color, float alpha)
	{
		return new Color(color.r, color.g, color.b, alpha);
	}

	public static Vector3 WithZ(this Vector3 vector, float z)
	{
		return new Vector3(vector.x, vector.y, z);
	}
}
