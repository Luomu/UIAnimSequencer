using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

//[ExecuteInEditMode]
[CreateAssetMenu(menuName = "UI Animation Sequence", order = 1000)]
public class UIAnimSequence : ScriptableObject
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
        public string TargetPath;
        public float Delay;
        public float Duration;

        //This is a list so it's possible to expand to
        //multi-action steps if necessary
        public List<SequenceAction> Actions;
    }

    [SerializeField]
    public List<SequenceStep> Elements = new List<SequenceStep>();

    // For easy access between animation player and animation sequence
    [NonSerialized]
    public UIAnimSequencePlayer AnimPlayer;

    //If tweens are created on awake, but not started, we need to toggle them later
    private List<LTDescr> m_createdTweens = new List<LTDescr>();

    public void RunSequence(UIAnimSequencePlayer player, bool dontStartYet = false)
    {
        if (player.Animating)
            return;

        if (dontStartYet)
        {
            m_createdTweens = new List<LTDescr>();
        }
        else if (player.HasDefaultState)
        {
            foreach (var tween in m_createdTweens)
            {
                tween.toggle = true;
            }
            player.Animating = true;
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
            if (string.IsNullOrEmpty(step.TargetPath))
            {
                Debug.LogError("Step has no target object");
                return;
            }

            Transform targetTf = player.FindTargetFromPath(step.TargetPath);
            var xform = targetTf as RectTransform;// step.Target.GetComponent<RectTransform>();
            if (xform == null)
            {
                Debug.LogError(step.TargetPath + " in " + player.transform.name + " has no RectTransform");
                return;
            }

            foreach (var action in step.Actions)
            {
                player.SaveActionOriginalState(step, action, xform);
            }
        }
        player.HasDefaultState = true;
        player.Animating = true;

        //Go through all sequence steps
        foreach (var step in Elements)
        {
            if (string.IsNullOrEmpty(step.TargetPath))
                continue;

            Transform targetTf = player.FindTargetFromPath(step.TargetPath);
            var xform = targetTf as RectTransform;
            //RectTransform xform = step.Target.GetComponent<RectTransform>();

            if (xform == null)
                continue;

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
                                descr = LeanTween.rotateAroundLocal(xform.gameObject, new Vector3(0, 0, 1), action.Float, step.Duration);
                            }
                            else
                            {
                                descr = LeanTween.rotateAroundLocal(xform.gameObject, new Vector3(0, 0, 1), action.Float, step.Duration);
                            }
                        }
                        break;

                    case ActionType.Scale:
                        {
                            if (action.Direction == Direction.From)
                            {
                                Vector3 tgtScale = xform.localScale;
                                xform.localScale = action.Vector;
                                descr = LeanTween.scale(xform.gameObject, tgtScale, step.Duration);
                            }
                            else
                            {
                                descr = LeanTween.scale(xform.gameObject, action.Vector, step.Duration);
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
                        var cg = xform.gameObject.GetComponent<CanvasGroup>();
                        var graphic = xform.gameObject.GetComponent<Graphic>();
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
                            descr = LeanTween.delayedCall(1f, () => xform.gameObject.SetActive(true));
                            descr.time = step.Duration; //todo check
                            break;
                        }
                    case ActionType.Hide:
                        {
                            descr = LeanTween.delayedCall(1f, () => xform.gameObject.SetActive(false));
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
                                    if (xform) action.LocalPosAtStart = xform.anchoredPosition3D;
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
                                    if (xform) xform.anchoredPosition3D = Vector3.LerpUnclamped(origPos, origPos + shakePosition, v);
                                }).setOnComplete(() =>
                                {
                                    if (xform) xform.anchoredPosition3D = action.LocalPosAtStart;
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
                    player.Animating = false;
                }
            }
        }
        //add one more call to trigger the end
        //Debug.Log("oncomplete will fire at " + totalDelay);
        LeanTween.delayedCall(totalDelay, player.OnCompleteTheWholeThing);
    }

    public void EditorForceUpdate(UIAnimSequencePlayer player)
    {
        if (player.Animating)
            LeanTween.update();
    }

    public void ResetToPreActionState(UIAnimSequencePlayer player, bool reset = true)
    {
        player.Animating = false;
        if (reset && player.HasDefaultState)
        {
            foreach (var step in Elements)
                foreach (var action in step.Actions)
                    player.ResetActionToInitialState(step, action);

            player.HasDefaultState = false;
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