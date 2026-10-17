using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelEditor
{
    public sealed partial class LevelEditorScreen
    {
        private LevelEditorDocument document;
        private TMP_InputField idInput, nameInput, cloneInput, iterationInput, thresholdInput;
        private TMP_Dropdown filesDropdown, goalsDropdown, comparisonDropdown;
        private Toggle lonelyToggle, crowdedToggle;
        private TextMeshProUGUI goalDetails, fileStatus;
        private GameObject countControls, positionControls, modal;
        private bool pendingSettings, pendingTarget, pendingFile;
        private bool refreshingFiles;
        private readonly List<string> fileIds = new List<string>();

        private void BuildConfigurationUI()
        {
            var root = (RectTransform)canvasObject.transform;
            Label(root, "ID", 336, 18, 32, 34, 16);
            idInput = TextInput(root, 370, 18, 175, "untitled", false);
            Label(root, "名称", 558, 18, 40, 34, 16);
            nameInput = TextInput(root, 602, 18, 160, "未命名关卡", false);
            Button(root, "保存", 775, 18, 85, 34, () => SaveDocument(false));
            Button(root, "另存为", 870, 18, 100, 34, () => SaveDocument(true));
            Button(root, "校验关卡", 980, 18, 110, 34, ValidateDraft);
            Label(root, "已有文件", 336, 62, 75, 30, 16);
            filesDropdown = Dropdown(root, 416, 60, 320, new[] { "请选择已保存的关卡" });
            filesDropdown.onValueChanged.AddListener(LoadSelected);
            Button(root, "刷新列表", 746, 60, 100, 32, () => RefreshFileList());
            fileStatus = Label(root, "", 968, 56, 450, 42, 14);
            idInput.characterLimit = 64; nameInput.characterLimit = 100;
            idInput.onValueChanged.AddListener(_ => { pendingFile = true; RefreshFileStatus(); });
            nameInput.onValueChanged.AddListener(_ => { pendingFile = true; RefreshFileStatus(); });
            var panel = Box(root, "Configuration", 0, 110, 332, 690, Panel);
            panel.anchorMin = panel.anchorMax = new Vector2(1, 1); panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-20, -110);
            RectTransform content = ScrollArea(panel, 0, 0, 332, 690, 1050);
            Label(content, "关卡配置", 16, 12, 280, 28, 20);
            lonelyToggle = RuleToggle(content, "孤独死亡（邻居 < 2）", 52);
            crowdedToggle = RuleToggle(content, "拥挤死亡（邻居 > 3）", 92);
            Label(content, "未触发死亡规则则保持存活。\n两项可都关闭；没有自动繁殖。", 16, 132, 280, 54, 15);
            Label(content, "存活分身上限", 16, 200, 158, 32, 17);
            cloneInput = TextInput(content, 178, 198, 118, "0", true);
            Label(content, "迭代轮数上限", 16, 246, 158, 32, 17);
            iterationInput = TextInput(content, 178, 244, 118, "1000", true);
            Label(content, "留空：分身 0 / 迭代 1000", 16, 286, 280, 26, 14);
            Button(content, "应用规则与上限", 16, 322, 280, 36, ApplySettings);
            cloneInput.onValueChanged.AddListener(_ => { pendingSettings = true; RefreshFileStatus(); });
            iterationInput.onValueChanged.AddListener(_ => { pendingSettings = true; RefreshFileStatus(); });
            lonelyToggle.onValueChanged.AddListener(_ => { pendingSettings = true; RefreshFileStatus(); });
            crowdedToggle.onValueChanged.AddListener(_ => { pendingSettings = true; RefreshFileStatus(); });
            Label(content, "通关目标 · 必须全部满足", 16, 382, 280, 28, 18);
            Button(content, "+ 角色位置", 16, 422, 136, 34, () => AddGoal(LevelIds.ActorAtPosition));
            Button(content, "+ 细胞覆盖", 160, 422, 136, 34, () => AddGoal(LevelIds.CoverPositions));
            Button(content, "+ 数量条件", 16, 466, 136, 34, () => AddGoal(LevelIds.CellCount));
            Button(content, "删除本目标", 160, 466, 136, 34, RemoveGoal);
            goalsDropdown = Dropdown(content, 16, 516, 280, new[] { "尚无目标" });
            goalsDropdown.onValueChanged.AddListener(SelectGoal);
            goalDetails = Label(content, "", 16, 560, 280, 164, 15);
            goalDetails.alignment = TextAlignmentOptions.TopLeft;
            goalDetails.overflowMode = TextOverflowModes.Ellipsis;
            var pc = Box(content, "Position controls", 16, 738, 280, 100, Panel);
            positionControls = pc.gameObject;
            Button(pc, "在棋盘设置", 0, 0, 136, 34, BeginGoalPaint);
            Button(pc, "移除目标格", 144, 0, 136, 34, () => SelectBrush(LevelBrush.EraseSelectedGoal));
            Button(pc, "清空本目标坐标", 0, 44, 280, 34, () => Session.ClearSelectedGoalPositions());
            var cc = Box(content, "Count controls", 16, 738, 280, 100, Panel);
            countControls = cc.gameObject;
            comparisonDropdown = Dropdown(cc, 0, 0, 96, new[] { ">", "<" });
            thresholdInput = TextInput(cc, 108, 0, 172, "", true);
            comparisonDropdown.onValueChanged.AddListener(_ => { pendingTarget = true; RefreshFileStatus(); });
            thresholdInput.onValueChanged.AddListener(_ => { pendingTarget = true; RefreshFileStatus(); });
            Button(cc, "应用数量条件", 0, 44, 280, 34, () => ApplyTargetIfNeeded());
            Label(content, "G：角色终点 · T：普通细胞覆盖\nT 可有多个格子，须同时覆盖。\n玩家不计入细胞数量，分身计入。", 16, 856, 280, 84, 15);
            Label(content, "保存会应用当前规则、上限和数量输入。\n草稿可不完整，校验后再制作试玩。", 16, 956, 280, 74, 14);
            ReadAllControls(); RefreshFileList();
        }

        private bool HasPendingEdits => document.IsDirty || pendingFile || pendingSettings || pendingTarget
            || widthInput.text != Session.Draft.grid.width.ToString() || heightInput.text != Session.Draft.grid.height.ToString();
        private void RefreshFileStatus()
        {
            if (!fileStatus) return;
            fileStatus.text = (HasPendingEdits ? "● 未保存  " : "已保存  ") + (document.SavedId ?? "新草稿") + "\n目录：Assets/StreamingAssets/Levels";
        }
        private void ReadAllControls()
        {
            widthInput.SetTextWithoutNotify(Session.Draft.grid.width.ToString());
            heightInput.SetTextWithoutNotify(Session.Draft.grid.height.ToString());
            idInput.SetTextWithoutNotify(Session.Draft.id ?? ""); nameInput.SetTextWithoutNotify(Session.Draft.name ?? "");
            cloneInput.SetTextWithoutNotify(Session.Draft.cloneLimit.ToString());
            iterationInput.SetTextWithoutNotify(Session.Draft.iterationLimit.ToString());
            lonelyToggle.SetIsOnWithoutNotify(Session.Draft.rules.Exists(r => r.id == LevelIds.LonelyDeath));
            crowdedToggle.SetIsOnWithoutNotify(Session.Draft.rules.Exists(r => r.id == LevelIds.CrowdedDeath));
            pendingSettings = pendingTarget = pendingFile = false;
            RefreshConfigurationView();
        }
        private void RefreshConfigurationView()
        {
            if (!goalsDropdown) return;
            var goals = Session.Draft.winConditions.conditions;
            goalsDropdown.ClearOptions();
            goalsDropdown.AddOptions(goals.Count == 0 ? new List<string> { "尚无目标" }
                : goals.Select((g, i) => (i + 1) + ". " + GoalName(g.type)).ToList());
            goalsDropdown.SetValueWithoutNotify(Math.Max(0, Session.SelectedGoalIndex));
            goalsDropdown.RefreshShownValue(); goalsDropdown.interactable = goals.Count > 0;
            int index = Session.SelectedGoalIndex;
            bool valid = index >= 0 && index < goals.Count;
            var goal = valid ? goals[index] : null;
            bool count = valid && goal.type == LevelIds.CellCount;
            countControls.SetActive(count); positionControls.SetActive(valid && !count);
            if (!valid) goalDetails.text = "添加或选择一个目标。\n位置目标可在棋盘上点击设置。\n所有目标必须同时成立。";
            else if (count)
            {
                goalDetails.text = "统计普通细胞（含分身），不含玩家。\n只支持严格大于或小于；等于阈值不算满足。\n阈值留空可保存为未完成草稿。";
                if (!pendingTarget)
                {
                    thresholdInput.SetTextWithoutNotify(goal.value?.ToString() ?? "");
                    comparisonDropdown.SetValueWithoutNotify(goal.@operator == "<" ? 1 : 0);
                    comparisonDropdown.RefreshShownValue();
                }
            }
            else if (goal.type == LevelIds.ActorAtPosition)
                goalDetails.text = "角色到达指定格子。\n当前坐标：" + (goal.position == null ? "未设置" : FormatPosition(goal.position)) + "\n点击“在棋盘设置”后点一个有效地块。";
            else
                goalDetails.text = "普通细胞同时覆盖所有目标格。\n已设置 " + (goal.positions?.Count ?? 0) + " 个：\n"
                    + (goal.positions == null ? "" : string.Join(" ", goal.positions.Select(FormatPosition)))
                    + "\n可用“移除目标格”逐格擦除。";
            RefreshFileStatus();
        }
        private static string FormatPosition(GridPosition p) { return "(" + p.x + "," + p.y + ")"; }
        private static string GoalName(string type)
        { return type == LevelIds.ActorAtPosition ? "角色位置" : type == LevelIds.CoverPositions ? "细胞覆盖" : "数量条件"; }
        private bool ApplySettingsInternal()
        {
            if (!Session.TryConfigure(lonelyToggle.isOn, crowdedToggle.isOn, cloneInput.text, iterationInput.text, out var message))
            { SetStatus(message, true); return false; }
            pendingSettings = false;
            cloneInput.SetTextWithoutNotify(Session.Draft.cloneLimit.ToString());
            iterationInput.SetTextWithoutNotify(Session.Draft.iterationLimit.ToString());
            SetStatus(message); RefreshFileStatus(); return true;
        }
        private void ApplySettings() { ApplySettingsInternal(); }
        private bool ApplyTargetIfNeeded()
        {
            if (!pendingTarget) return true;
            if (!Session.TrySetCountGoal(comparisonDropdown.value == 0 ? ">" : "<", thresholdInput.text, out var message))
            { SetStatus(message, true); return false; }
            pendingTarget = false; RefreshConfigurationView(); SetStatus(message); return true;
        }
        private void AddGoal(string type)
        {
            if (!ApplyTargetIfNeeded()) return;
            Session.AddGoal(type); RefreshConfigurationView();
            if (type != LevelIds.CellCount) BeginGoalPaint();
        }
        private void SelectGoal(int index)
        {
            if (!ApplyTargetIfNeeded())
            { goalsDropdown.SetValueWithoutNotify(Math.Max(0, Session.SelectedGoalIndex)); goalsDropdown.RefreshShownValue(); return; }
            Session.SelectGoal(index); pendingTarget = false; RefreshConfigurationView();
        }
        private void RemoveGoal()
        {
            pendingTarget = false; Session.RemoveSelectedGoal(); RefreshConfigurationView();
        }
        private void BeginGoalPaint()
        {
            int index = Session.SelectedGoalIndex;
            if (index < 0) { SetStatus("请先选择一个位置目标。", true); return; }
            var goal = Session.Draft.winConditions.conditions[index];
            if (goal.type == LevelIds.CellCount) return;
            SelectBrush(goal.type == LevelIds.ActorAtPosition ? LevelBrush.ActorGoal : LevelBrush.CellGoal);
            SetStatus("点击有效地块，设置当前选中目标；细胞覆盖可点击多个格子。");
        }
        private bool PrepareSave()
        {
            if (widthInput.text != Session.Draft.grid.width.ToString() || heightInput.text != Session.Draft.grid.height.ToString())
            { SetStatus("网格尺寸有未应用输入，请先点击居中调整尺寸。", true); return false; }
            if (!LevelRepository.IsValidId(idInput.text))
            { SetStatus("ID 需为 1～64 位英文字母、数字、下划线或连字符，首位为字母或数字。", true); return false; }
            return ApplyTargetIfNeeded() && (!pendingSettings || ApplySettingsInternal());
        }
        private void SaveDocument(bool saveAs)
        {
            if (!PrepareSave()) return;
            string id = idInput.text, name = nameInput.text;
            Action save = () => Guard(() =>
            {
                string path = document.Save(id, name, saveAs);
                ReadAllControls(); RefreshFileList(); Refresh();
                var validation = LevelValidator.ValidateForPlay(Session.Draft);
                SetStatus("已保存：" + path + (validation.CanPlay ? "" : "\n这是未完成草稿：" + validation.Errors.Count + " 项需完善，可点击校验关卡查看。"));
            });
            if (!saveAs && document.SavedId == null && File.Exists(document.Repository.PathFor(id)))
                ShowDialog("覆盖已有文件？", "将覆盖 " + id + ".json。请选择确认，或取消后使用新的 ID 另存为。", save);
            else save();
        }
        private void RefreshFileList(bool resetSelection = false)
        {
            refreshingFiles = true;
            try
            {
                string[] ids;
                try { ids = document.Repository.ListIds(); }
                catch (Exception e) when (IsFileError(e))
                { ids = Array.Empty<string>(); SetStatus("刷新列表失败：" + e.Message, true); }
                fileIds.Clear(); fileIds.AddRange(ids);
                filesDropdown.ClearOptions();
                filesDropdown.AddOptions(new List<string> { "请选择已保存的关卡" }.Concat(fileIds).ToList());
                int selected = resetSelection ? -1 : fileIds.IndexOf(document.SavedId);
                filesDropdown.SetValueWithoutNotify(selected < 0 ? 0 : selected + 1); filesDropdown.RefreshShownValue();
            }
            finally { refreshingFiles = false; }
        }
        private void LoadSelected(int selection)
        {
            if (refreshingFiles || selection <= 0 || selection > fileIds.Count) return;
            string id = fileIds[selection - 1];
            // Check stale/deleted/corrupt entries before asking to discard any current edits.
            try
            {
                var candidate = document.Repository.Load(id);
                new LevelEditorSession().ReplaceDraft(candidate);
            }
            catch (Exception e) when (IsFileError(e)) { RecoverFileList(e); return; }
            filesDropdown.SetValueWithoutNotify(0); filesDropdown.RefreshShownValue();
            ConfirmDiscard(() =>
            {
                try
                {
                    // Read again after confirmation: the file may have changed while the dialog was open.
                    document.Load(id); pendingTarget = false;
                    ReadAllControls(); Refresh();
                    int index = fileIds.IndexOf(id);
                    filesDropdown.SetValueWithoutNotify(index < 0 ? 0 : index + 1); filesDropdown.RefreshShownValue();
                    SetStatus("已加载：" + document.Repository.PathFor(id));
                }
                catch (Exception e) when (IsFileError(e)) { RecoverFileList(e); }
            });
        }
        private void RecoverFileList(Exception error)
        {
            RefreshFileList(true);
            SetStatus("加载失败：" + error.Message + "\n已刷新文件列表，请重新选择；当前草稿保持不变。", true);
        }
        private static bool IsFileError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is FormatException
            || e is ArgumentException || e is InvalidOperationException || e is System.Security.SecurityException;
        private void ValidateDraft()
        {
            if (!PrepareSave()) return;
            var candidate = LevelJson.DeepCopy(Session.Draft); candidate.id = idInput.text; candidate.name = nameInput.text;
            var result = LevelValidator.ValidateForPlay(candidate);
            string body = "错误 " + result.Errors.Count + " 项；提示 " + result.Warnings.Count + " 项。\n\n";
            body += string.Join("\n", result.Errors.Select(e => "[错误] " + e).Concat(result.Warnings.Select(w => "[提示] " + w)));
            if (result.CanPlay) body += "\n\n数据校验通过，可点击左下方“试玩当前草稿”，无需先保存 JSON。";
            ShowDialog("关卡校验", body);
        }
        private void ConfirmDiscard(Action action)
        {
            if (HasPendingEdits) ShowDialog("放弃未保存的编辑？", "加载或新建会替换当前草稿和未应用输入。请先保存需要保留的内容。", action);
            else action();
        }
        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException
                || e is ArgumentException || e is InvalidOperationException || e is System.Security.SecurityException)
            { SetStatus("操作失败：" + e.Message, true); }
        }
        private void ShowDialog(string title, string text, Action confirm = null)
        {
            CloseDialog();
            var overlay = Box((RectTransform)canvasObject.transform, "Modal", 0, 0, 0, 0, new Color(0, 0, 0, .78f));
            Stretch(overlay); modal = overlay.gameObject;
            var window = Box(overlay, "Dialog", 0, 0, 820, 520, Panel);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); window.anchoredPosition = Vector2.zero;
            Label(window, title, 24, 18, 772, 38, 24);
            float height = Math.Max(316, text.Split('\n').Length * 30 + text.Length / 48 * 24);
            var content = ScrollArea(window, 24, 72, 772, 344, height);
            var body = Label(content, text, 8, 0, 732, height, 17); body.alignment = TextAlignmentOptions.TopLeft;
            Button(window, confirm == null ? "关闭" : "取消", 24, 454, 210, 40, CloseDialog);
            if (confirm != null) Button(window, "确认", 566, 454, 230, 40, () => { CloseDialog(); confirm(); });
        }
        private void CloseDialog()
        {
            if (!modal) return;
            modal.SetActive(false); Destroy(modal); modal = null;
        }
        private TMP_InputField TextInput(RectTransform parent, float x, float y, float width, string initial, bool integer)
        {
            var rect = Box(parent, "Input", x, y, width, 34, new Color(.18f, .25f, .34f));
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vr = (RectTransform)viewport.transform; vr.SetParent(rect, false); Stretch(vr);
            vr.offsetMin = new Vector2(7, 2); vr.offsetMax = new Vector2(-7, -2);
            var text = Label(vr, "", 0, 0, width - 14, 30, 17); Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            field.textViewport = vr; field.textComponent = text; field.targetGraphic = rect.GetComponent<Image>();
            field.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine; field.characterLimit = integer ? 10 : 100;
            field.text = initial;
            return field;
        }
        private Toggle RuleToggle(RectTransform parent, string text, float y)
        {
            var rect = Box(parent, text, 16, y, 280, 32, new Color(.16f, .22f, .3f));
            var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = rect.GetComponent<Image>();
            var mark = Box(rect, "Check", 7, 7, 18, 18, new Color(.3f, .83f, .72f));
            mark.GetComponent<Image>().raycastTarget = false; toggle.graphic = mark.GetComponent<Image>();
            Label(rect, text, 34, 0, 240, 32, 17);
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            return toggle;
        }
        private TMP_Dropdown Dropdown(RectTransform parent, float x, float y, float width, string[] options)
        {
            var rect = Box(parent, "Dropdown", x, y, width, 34, new Color(.16f, .22f, .3f));
            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
            dropdown.captionText = Label(rect, "", 8, 0, width - 38, 34, 16);
            dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis; dropdown.captionText.enableWordWrapping = false;
            Label(rect, "v", width - 25, 0, 20, 34, 16);
            var template = Box(rect, "Template", 0, 0, width, 220, new Color(.12f, .18f, .26f));
            template.anchorMin = new Vector2(0, 0); template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(.5f, 1); template.sizeDelta = new Vector2(0, 220); template.anchoredPosition = new Vector2(0, -2);
            var content = ScrollArea(template, 2, 2, width - 4, 216, 32);
            var item = Box(content, "Item", 0, 0, width - 20, 32, new Color(.18f, .26f, .35f));
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var mark = Box(item, "Selected", 2, 6, 4, 20, new Color(.3f, .83f, .72f));
            mark.GetComponent<Image>().raycastTarget = false; toggle.graphic = mark.GetComponent<Image>();
            dropdown.itemText = Label(item, "", 12, 0, width - 36, 32, 16);
            dropdown.itemText.enableWordWrapping = false; dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
            dropdown.template = template; template.gameObject.SetActive(false);
            dropdown.ClearOptions(); dropdown.AddOptions(options.ToList());
            return dropdown;
        }
        private RectTransform ScrollArea(RectTransform parent, float x, float y, float width, float height, float contentHeight)
        {
            var rect = Box(parent, "Scroll", x, y, width, height, Panel);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vr = (RectTransform)viewport.transform; vr.SetParent(rect, false); Stretch(vr);
            vr.offsetMax = new Vector2(-16, 0);
            var contentObject = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentObject.transform; content.SetParent(vr, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, contentHeight); content.anchoredPosition = Vector2.zero;
            var barRect = Box(rect, "Scrollbar", 0, 0, 10, height - 8, new Color(.12f, .18f, .27f));
            barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(1, .5f); barRect.sizeDelta = new Vector2(10, -8); barRect.anchoredPosition = new Vector2(-2, 0);
            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            var handle = Box(barRect, "Handle", 0, 0, 10, 20, new Color(.36f, .49f, .59f)); Stretch(handle);
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.viewport = vr; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            scroll.verticalScrollbar = bar; scroll.verticalNormalizedPosition = 1;
            return content;
        }
    }
}
