// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace UnityEditor.Experimental.UIElements
{
    public class EnumField : BaseField<Enum>
    {
        public new class UxmlFactory : UxmlFactory<EnumField, UxmlTraits> {}

        public new class UxmlTraits : BaseField<Enum>.UxmlTraits
        {
            UxmlStringAttributeDescription m_Type = new UxmlStringAttributeDescription { name = "type", use = UxmlAttributeDescription.Use.Required};
            UxmlStringAttributeDescription m_Value = new UxmlStringAttributeDescription { name = "value" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                EnumField enumField = (EnumField)ve;
                // Only works for the currently running assembly
                enumField.m_EnumType = Type.GetType(m_Type.GetValueFromBag(bag));
                if (enumField.m_EnumType != null)
                {
                    string v = m_Value.GetValueFromBag(bag);

                    if (!Enum.IsDefined(enumField.m_EnumType, v))
                    {
                        Debug.LogErrorFormat("Could not parse value of '{0}', because it isn't defined in the {1} enum.", v, enumField.m_EnumType.FullName);
                        enumField.value = null;
                    }
                    else
                    {
                        enumField.value = (Enum)Enum.Parse(enumField.m_EnumType, v);
                    }
                }
            }
        }

        private Type m_EnumType;
        private TextElement m_TextElement;

        public override Enum value
        {
            get { return m_Value; }
            set
            {
                if (m_Value != value)
                {
                    m_Value = value;
                    m_TextElement.text = ObjectNames.NicifyVariableName(m_Value.ToString());

                    using (ChangeEvent<Enum> evt = ChangeEvent<Enum>.GetPooled(m_Value, value))
                    {
                        evt.target = this;
                        SetValueWithoutNotify(value);
                        UIElementsUtility.eventDispatcher.DispatchEvent(evt, panel);
                    }

                    IncrementVersion(VersionChangeType.Repaint);
                }
            }
        }

        public string text
        {
            get { return m_TextElement.text; }
        }

        private void Initialize(Enum defaultValue)
        {
            m_TextElement = new TextElement();
            Add(m_TextElement);
            if (defaultValue != null)
            {
                Init(defaultValue);
            }
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
        }

        public EnumField()
        {
            Initialize(null);
        }

        public EnumField(Enum defaultValue)
        {
            Initialize(defaultValue);
        }

        public void Init(Enum defaultValue)
        {
            m_EnumType = defaultValue.GetType();
            value = defaultValue;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse)
                ShowMenu();
        }

        [Obsolete("This method is replaced by simply using this.value. The default behaviour has been changed to notify when changed. If the behaviour is not to be notified, SetValueWithoutNotify() must be used.", false)]
        public override void SetValueAndNotify(Enum newValue)
        {
            if (value != newValue)
            {
                value = newValue;
            }
        }

        protected internal override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if ((evt.GetEventTypeId() == KeyDownEvent.TypeId()) &&
                (evt as KeyDownEvent)?.character == '\n')
            {
                ShowMenu();
                evt.StopPropagation();
            }
        }

        private void ShowMenu()
        {
            if (m_EnumType == null)
                return;

            var menu = new GenericMenu();

            foreach (Enum item in Enum.GetValues(m_EnumType))
            {
                bool isSelected = item.CompareTo(value) == 0;
                string label = ObjectNames.NicifyVariableName(item.ToString());
                menu.AddItem(new GUIContent(label), isSelected,
                    contentView => ChangeValueFromMenu(contentView),
                    item);
            }

            var menuPosition = new Vector2(0.0f, layout.height);
            menuPosition = this.LocalToWorld(menuPosition);
            var menuRect = new Rect(menuPosition, Vector2.zero);
            menu.DropDown(menuRect);
        }

        private void ChangeValueFromMenu(object menuItem)
        {
            value = menuItem as Enum;
        }
    }
}
