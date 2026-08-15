using UnityEngine;
using System.Reflection;
//using Colorful;
using System;
using System.Linq.Expressions;

namespace VisSim
{


    [HelpURL("http://http://www.ucl.ac.uk/~smgxprj")]
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    abstract public class LinkableBaseEffect : BaseEffect
    {
        // required Methods
        protected abstract void OnUpdate();
        protected abstract override void OnRenderImage(RenderTexture source, RenderTexture destination);

        // To link effect parameter-values across eyes (left/right)
        public bool LinkEyes = true;

        // Handles
        protected bool isLeftEye;
        private LinkableBaseEffect rightEyeEffectInstance;
        private LinkableBaseEffect leftEyeEffectInstance;

        /// <summary>
        /// This effect's counterpart on the other eye, or null before the pair has
        /// been resolved.
        ///
        /// Exposed because linking only copies fields marked [Linkable], which is
        /// not enough for effects configured through a METHOD rather than a field.
        /// myFieldLoss is the case in point: setGrid() both stores the grid and
        /// pushes the generated texture into this instance's Material, and the
        /// Material is per-instance. The twin therefore rendered with no overlay --
        /// enabled, correct, and invisible -- which is why Vision Loss did nothing
        /// on its own while scalar-only effects worked.
        /// </summary>
        protected LinkableBaseEffect TwinEyeEffect
        {
            get { return isLeftEye ? rightEyeEffectInstance : leftEyeEffectInstance; }
        }

        // Cache all fields marked with LinkableAttribute to avoid expensive
        // reflection every frame in Update(). This significantly reduces the
        // overhead when running the simulator in the background.
        private FieldInfo[] linkableFields;
        private Action<LinkableBaseEffect, LinkableBaseEffect>[] linkableFieldCopiers;
        

        /*
		public enum EyeType
		{
			LeftEye = 0,
			RightEye = 1,
			Neither = 2,
		}
		[TweakableMember(0,1, "mastereye", "myBrightnessContrastGamma")]
		public EyeType MasterEye = EyeType.LeftEye;
		*/


        public void OnEnable()
        {
            // ensure material is initialised
            Material.GetType();

            // check if this is the left eye effect
            isLeftEye = this.gameObject.tag == "LeftEye";

            // Check effect is present left eye
            GameObject leftEye = GameObject.FindWithTag("LeftEye");
            Component[] leftEyeEffectInstances = leftEye.GetComponentsInChildren(this.GetType());
            if (leftEyeEffectInstances.Length != 1)
            {
                Debug.LogError(this.GetType() + " disabled: 1, and only 1 instance of expected effect required on LEFT EYE.");
                this.enabled = false;
                return;
            }

            // Check effect is present on right eye
            GameObject rightEye = GameObject.FindWithTag("RightEye");
            Component[] rightEyeEffectInstances = rightEye.GetComponentsInChildren(this.GetType());
            if (rightEyeEffectInstances.Length != 1)
            {
                Debug.LogError(this.GetType() + " disabled: 1, and only 1 instance of expected effect required on RIGHT EYE.");
                this.enabled = false;
                return;
            }

            // store references
            leftEyeEffectInstance = leftEyeEffectInstances[0] as LinkableBaseEffect;
            rightEyeEffectInstance = rightEyeEffectInstances[0] as LinkableBaseEffect;

            // Cache all fields that are marked as linkable so we don't have to
            // iterate over every field each frame via reflection.
            Type effectType = GetType();
            linkableFields = Array.FindAll(effectType.GetFields(), fi => fi.IsDefined(typeof(LinkableAttribute), false) && !fi.IsStatic);
            linkableFieldCopiers = new Action<LinkableBaseEffect, LinkableBaseEffect>[linkableFields.Length];
            for (int i = 0; i < linkableFields.Length; i++)
            {
                linkableFieldCopiers[i] = CreateFieldCopier(linkableFields[i]);
            }

            // also enable right eye, if the two eyes are locked
            if (isLeftEye && this.LinkEyes)
            {
                rightEyeEffectInstance.enabled = true;
            }
        }

        protected override void OnDisable()
        {
            // also disable right eye, if the two eyes are locked
            if (isLeftEye && this.LinkEyes && (rightEyeEffectInstance!=null)) // (i.e., may be null if failed to enable in the first place)
            {
                rightEyeEffectInstance.enabled = false;
            }

            // call BaseEffect method
            base.OnDisable();
        }

        public void Update()
        {
            // enable if not done so already (e.g., if user forgot to include base.onEnable() in subclass!)
            if (leftEyeEffectInstance == null || rightEyeEffectInstance == null) // ||(isLeftEye && leftEyeEffectInstance.enabled == false)
            {
                this.OnEnable();
                if (!this.enabled) { return; }
            }

            //Debug.Log (this.gameObject.tag);
            if (isLeftEye)
            {
                // Sync lock value across eyes without using reflection each frame
                rightEyeEffectInstance.LinkEyes = this.LinkEyes;

                // If LinkEyes, then set all LinkableAttribute fields to have the value of the left eye
                if (this.LinkEyes)
                {
                    rightEyeEffectInstance.enabled = leftEyeEffectInstance.enabled;

                    for (int i = 0; i < linkableFieldCopiers.Length; i++)
                    {
                        linkableFieldCopiers[i](this, rightEyeEffectInstance);
                    }
                }
            }
            else
            {
                // Read LinkEyes state from left eye directly
                this.LinkEyes = leftEyeEffectInstance.LinkEyes;
                if (this.LinkEyes)
                {
                    rightEyeEffectInstance.enabled = leftEyeEffectInstance.enabled;
                }
            }
            
            // Call OnUpdate
            OnUpdate();
        }

        protected override string GetShaderName()
        {
            return "Hidden/VisSim/LinkableBaseEffect (this should be overriden)";
        }

        private static Action<LinkableBaseEffect, LinkableBaseEffect> CreateFieldCopier(FieldInfo fieldInfo)
        {
            Type declaringType = fieldInfo.DeclaringType ?? typeof(LinkableBaseEffect);

            ParameterExpression sourceParameter = Expression.Parameter(typeof(LinkableBaseEffect), "source");
            ParameterExpression targetParameter = Expression.Parameter(typeof(LinkableBaseEffect), "target");

            UnaryExpression typedSource = Expression.Convert(sourceParameter, declaringType);
            UnaryExpression typedTarget = Expression.Convert(targetParameter, declaringType);

            MemberExpression sourceField = Expression.Field(typedSource, fieldInfo);
            MemberExpression targetField = Expression.Field(typedTarget, fieldInfo);

            BinaryExpression assignExpression = Expression.Assign(targetField, sourceField);

            return Expression.Lambda<Action<LinkableBaseEffect, LinkableBaseEffect>>(assignExpression, sourceParameter, targetParameter).Compile();
        }
    }
}


/// Attribute that can be used to mark fields or properties on MonoBehaivours as Linkable
public class LinkableAttribute : Attribute
{
    //String name;
    public LinkableAttribute()
    {
        //this.name = "default";
    }
    /*
    public LinkableAttribute(String name)
    {
        this.name = name;
    }
    */
}