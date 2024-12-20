﻿var idPrefix = "message_";

var messageType = ["Information", "Question", "Warning", "Error", "In", "Out"];

const MessageType = {
	Information: 0,
	Question: 1,
	Warning: 2,
	Error: 3,
	In: 4,
	Out: 5,
}


const AttachmentSendType = {
	None: 0,
	Temp: 1,
	User: 2,
}

const ContextType = {
	None: 0,
	Clipboard: 1,
	Selection: 2,
	ActiveDocument: 4,
	OpenDocuments: 8192,
	SelectedDocuments: 8,
	ActiveProject: 16,
	SelectedProject: 32,
	Solution: 64,
	ChatHistory: 256,
	Error: 512,
	ErrorDocument: 1024,
	Exception: 2048,
	ExceptionDocuments: 4096,
	Image: 8192,
	Audio: 16384,
}

function enumToString(value, enumObject) {
	for (let key in enumObject) {
		if (enumObject[key] === value) {
			return key;
		}
	}
	return null;
}

function SetZoom(zoom) {
	var scale = zoom / 100;
	var el = document.getElementById('chatLog');
	el.style.zoom = zoom + "%";
	if (isInsideApp) {
		el.style.width = 100 / scale + "%"; // Adjust width to fill the parent.
		//var elP = document.getElementById('chatLogParent');
		//elP.style.overflowX = "hidden";
		//elP.style.overflowY = "hidden";
	}
}

function InsertMessage(message, autoScroll) {
	var chatLog = document.getElementById('chatLog');
	var messageHTML = CreateMessageHtml(message)
	chatLog.insertAdjacentHTML('beforeend', messageHTML);
	// Scroll to the bottom of the chatLog div.
	if (autoScroll && keepScrollOnTheBottom)
		ScrollToBottom();
	UpdateRegenerateButtons();
}

function isEmpty(s) {
	return s === null || s === undefined || s === "";
}

function UpdateMessageStatus(messageId, status) {
	var id = idPrefix + messageId + "_status";
	console.log("el[" + id + "].textContent: " + status);
	var statusEl = document.getElementById(id);
	if (!statusEl) {
		console.log("Error: el[" + id + "] element not found");
	}
	var statusTextEl = statusEl.querySelector('.status-text');
	// Update the text content
	statusTextEl.textContent = status;
	// Show/Hide the status element
	SetVisible(statusEl, !isEmpty(status));
}

function DeleteMessage(messageId) {
	var messageEl = document.getElementById(idPrefix + messageId);
	messageEl.parentElement.removeChild(messageEl);
	UpdateRegenerateButtons();
	UpdateKeepScrollOnTheBottom();
}

function DeleteMessages() {
	var chatLog = document.getElementById('chatLog');
	chatLog.innerHTML = "";
	UpdateKeepScrollOnTheBottom();
}

function UpdateMessage(message, autoScroll) {
	var messageEl = document.getElementById(idPrefix + message.Id);
	if (!messageEl)
		return false;
	// Get the parent element (chatLog)
	var chatLog = document.getElementById('chatLog');
	// Find the index of messageEl in chatLog's childNodes
	var children = chatLog.children;
	var index = Array.prototype.indexOf.call(children, messageEl);
	// Remove the existing message element
	chatLog.removeChild(messageEl);
	// Create new message element.
	var messageHTML = CreateMessageHtml(message)
	// Insert the new message element at the position index
	if (index >= 0 && index < children.length) {
		children[index].insertAdjacentHTML('beforebegin', messageHTML);
	} else {
		chatLog.insertAdjacentHTML('beforeend', messageHTML);
	}
	UpdateRegenerateButtons();
	// Scroll if needed
	if (autoScroll === undefined)
		autoScroll = true;
	if (autoScroll && keepScrollOnTheBottom)
		ScrollToBottom();
	return true;
}

function CreateMessageHtml(message) {

	// Convert markdown code to HTML.
	var body = "" + message.Body;
	// According to the Markdown Guide, in order to force a new line, you need to add 2 spaces at the end of the line, followed by the return key.
	// https://markdown-guide.readthedocs.io/en/latest/basics.html#line-return
	//var rx = new RegExp("([^ ])([ ]{0,1})(\\r?\\n)", "g");
	//body = body.replace(rx, "$1  $3");
	// Add support for markdown.
	var spacesRx = new RegExp("\\s+$", "g")
	body = body.replace(spacesRx, "");
	// Make sure characters like `<` or `>` are not treated as HTML element characters.
	body = ConvertForDisplayAsHtml(body);
	body = parseMarkdown(body, true);
	// Trim all space from the end.
	body = body.replace(spacesRx, "");
	// Make sure that multiple spaces are preserved.
	//if (message.Type == MessageType.Out)
	//	body = "<div style=\"white-space: pre;\">" + body + "</div>";
	var messageInstructionsHTML = "";
	var buttons = "";
	// If body AI instructions supplied then...
	if ((message.BodyInstructions) && ("" + message.BodyInstructions).length > 0) {
		var instructions = message.BodyInstructions;
		// Make sure characters like `<` or `>` are not treated as HTML element characters.
		instructions = ConvertForDisplayAsHtml(instructions);
		instructions = parseMarkdown(instructions);
		var box = AddAttachmentBox(idPrefix + message.Id + "_instructions", "Instructions", instructions, "", false);
		buttons += box.buttonHTML;
		messageInstructionsHTML = box.panelHTML;
	}
	var attachments = message.Attachments;
	var data = "";
	if (Array.isArray(attachments)) {
		for (var i = 0; i < attachments.length; i++) {
			var a = attachments[i];
			var aData = "";
			var displayPanelContent = false;
			// If data use markdown then...
			// Example: ```{Language}\r\n{Data}\r\n```
			console.log("a.ContextType: " + a.Type);
			if (a.Type === ContextType.Image) {
				displayPanelContent = true;
				console.log(a.Data);
				var imageInfo = JSON.parse(a.Data);
				var imageFullPath = GetItemPath(imageInfo.Name);
				var link = document.createElement("a");
				link.href = imageFullPath;
				link.target = "_blank";
				var img = document.createElement('img');
				img.alt = imageInfo.Prompt;
				img.style.width = Math.round(imageInfo.Width / 2) + "px";
				img.style.height = Math.round(imageInfo.Height / 2) + "px";
				img.src = (imageInfo.DataUri)
					? imageInfo.DataUri
					: imageFullPath;
				link.appendChild(img);
				aData = link.outerHTML;
			}
			else if (a.Type === ContextType.Audio) {
				displayPanelContent = true;
				console.log(a.Data);
				var audioInfo = JSON.parse(a.Data);
				var audioFullPath = GetItemPath(audioInfo.Name);
				var audioDiv = document.createElement("div");
				try {
					var audio = document.createElement('audio');
					audio.controls = true;
					audio.src = (audioInfo.DataUri)
						? audioInfo.DataUri
						: audioFullPath;
					audioDiv.appendChild(audio);
				} catch (e) {
					audioDiv.innerText = 'Error:' + e.message + '\r\n' + e.stack;
				}
				aData = audioDiv.outerHTML;
			}
			else if (a.IsMarkdown) {
				aData = (a.Data) ? a.Data : "";
				aData = ConvertForDisplayAsHtml(aData);
				aData = parseMarkdown(aData);
			} else {
				// Wrap into plain text element.
				var pre = document.createElement('pre');
				pre.innerText = a.Data;
				aData = pre.outerHTML;
			}
			var aInstructions = "";
			// If instructions supplied then...
			if ((a.Instructions) && ("" + a.Instructions).length > 0) {
				aInstructions = a.Instructions;
				aInstructions = ConvertForDisplayAsHtml(aInstructions);
				aInstructions = aInstructions + "</ br>";
			}
			var aTitle = a.Title;
			if (a.SendType === AttachmentSendType.Temp) {
				aTitle += " ᵀ";
			}
			if (a.SendType === AttachmentSendType.User) {
				aTitle += " ᵁ";
			}
			var box = AddAttachmentBox(idPrefix + message.Id + "_" + a.Id, aTitle, aInstructions, aData, displayPanelContent, a.Type);
			buttons += box.buttonHTML;
			data += "\r\n\r\n" + box.panelHTML;
		}
	}
	var bodyContents = "" + messageInstructionsHTML + body;
	// Replace all placeholders with actual values from the message.
	var messageTemplate = document.getElementById('messageTemplate').innerHTML;
	var messageHTML = messageTemplate
		.replace(/{Type}/g, messageType[message.Type])
		.replace(/{Automated}/g, message.IsAutomated ? "Automated" : "Normal")
		.replace(/{Id}/g, idPrefix + message.Id)
		.replace(/{User}/g, message.User)
		.replace(/{Date}/g, FormatDateTime(message.Date))
		.replace(/{Body}/g, bodyContents)
		.replace(/{BodyHide}/g, isEmpty(bodyContents) ? "display-none" : "")
		.replace(/{Data}/g, data)
		.replace(/{DataHide}/g, isEmpty(data) ? "display-none" : "")
		.replace(/{Buttons}/g, buttons);
	return messageHTML;
}


function UpdateRegenerateButtons() {
	// Get all the buttons inside chatLog
	var buttons = document.getElementById('chatLog').getElementsByClassName('regenerate-Out')
	// Hide/Show all buttons
	var newList = [];

	// Exclude hidden buttons.
	for (var i = 0; i < buttons.length; i++) {
		if (buttons[i].classList.contains("item-Automated"))
			continue;
		newList.push(buttons[i]);
	}
	buttons = newList;

	for (var i = 0; i < buttons.length; i++) {
		var value = 'inline-block';
		// If button is last.
		//var value = i == buttons.length - 1 ? 'inline-block' : "none";
		var style = buttons[i].style;
		if (style.display !== value)
			style.display = value;
	}
}

/*
	id = "{messageId}_{attachmentId}".
*/
function AddAttachmentBox(id, title, instructions, data, displayPanelContent, contextType) {
	var showInstructions = (instructions) && instructions.trim().length > 0;
	var showData = (data) && data.trim().length > 0;
	// Return if nothing to show.
	if (!showInstructions && !showData)
		return { buttonHTML: "", panelHTML: "" };
	// Show hide attachment buttons.
	var buttonHTML = document.getElementById("expandableBoxShowHideDataButtonTemplate").innerHTML
		.replace(/{Id}/g, id)
		.replace(/{Title}/g, title);

	var titleButtonsHTML = "";
	if (contextType === ContextType.Image || contextType === ContextType.Audio) {
		titleButtonsHTML = document.getElementById("expandableBoxImageButtonTemplate").innerHTML
			.replace(/{Id}/g, id);
	}
	var panelHTML = document.getElementById("expandableBoxPanelTemplate").innerHTML
		.replace(/{Id}/g, id)
		.replace(/{Title}/g, title)
		.replace(/{PanelClass}/g, displayPanelContent ? "" : "display-none")
		.replace(/{Instructions}/g, instructions)
		.replace(/{InstructionsClass}/g, showInstructions ? "" : "display-none")
		.replace(/{Data}/g, data)
		.replace(/{DataClass}/g, showData ? "" : "display-none")
		.replace(/{TitleButtons}/g, titleButtonsHTML);
	return { buttonHTML: buttonHTML, panelHTML: panelHTML };
}

/*
	id = "{messageId}_{attachmentId}".
*/
function AttachmentButton_Click(sender, id) {
	if (sender !== null) {
		sender.classList.add('clicked');
		setTimeout(function () {
			sender.classList.remove('clicked');
		}, 500);
	}
	var button = document.getElementById(id + "_button");
	var panel = document.getElementById(id + "_panel");
	// Toggle visibility
	var isVisible = !panel.classList.contains("display-none");
	SetVisible(panel, !isVisible)
	if (isVisible) {
		button.classList.remove("expandable-button-visible");
	} else {
		button.classList.add("expandable-button-visible");
	}
}

function SetVisible(el, isVisible) {
	var isControlVisible = !el.classList.contains("display-none");
	console.log("SetVisible: " + isVisible + ", isControlVisible: " + isControlVisible);
	// if must show and is not visible then...
	if (isVisible && !isControlVisible) {
		el.classList.remove("display-none");
	}
	// If must hide and is visible then...
	else if (!isVisible && isControlVisible) {
		el.classList.add("display-none");
	}
	// Making items visible can expand content.
	// Make sure to keep scroll on the bottom.
	if (keepScrollOnTheBottom)
		ScrollToBottom();
}

/** Prepare text for markdown and display as HTML. */
function ConvertForDisplayAsHtml(input) {
	if (!input)
		return input;
	var lines = input.split(/\r?\n|\r/);
	var rx = new RegExp(/^\s*[`]{3}[a-z0-9\-]*\s*$/gmi);
	var insideBlock = false;
	for (var i = 0; i < lines.length; i++) {
		var line = lines[i];
		var isMark = rx.test(line);
		if (isMark) {
			// If block ends then...
			if (insideBlock)
				lines[i] = lines[i] + "\r\n<br />";
			insideBlock = !insideBlock;
			continue;
		}
		// Don't escape text inside block
		if (insideBlock)
			continue;
		var parts = line.split('`');
		for (var p = 0; p < parts.length; p++) {
			// Only process the text not within single quotes
			if (p % 2 === 0) {
				parts[p] = EscapeHtml(parts[p]);
			}
		}
		// Add extra new line for markdown to add `<br />` correcly.
		lines[i] = parts.join('`') + "\r\n";
	}
	var processedText = lines.join("\r\n");
	return processedText;
}

function EscapeHtml(unsafe) {
	return unsafe
		.replace(/&/g, "&amp;")
		.replace(/</g, "&lt;")
		.replace(/>/g, "&gt;")
		.replace(/"/g, "&quot;")
		.replace(/'/g, "&#039;");
}

var previousMessageTextBody = "";

/** Update message.
	@param messageId Unique message Id.
	@param responseDiff Response recevied from the stream.
 */
function UpdateMessageDiff(messageId, responseDiff) {
	// Get message element by message Id.
	var el = document.getElementById(idPrefix + messageId);
	var bodyEl = el.getElementsByClassName("chat-message-body")[0];
	var currentMessageTextBody = previousMessageTextBody + responseDiff;
	var currentMessageHtmlBody = parseMarkdown(currentMessageTextBody, true);
	ApplyDiffference(bodyEl, currentMessageHtmlBody);
	previousMessageTextBody = currentMessageTextBody;
}

function ApplyDiffference(el, newHtml) {
	var tempEl = el.cloneNode();
	// Convert the new html string to DOM tree
	tempEl.innerHTML = newHtml;
	let i = 0;
	for (; i < tempEl.childNodes.length && i < el.childNodes.length; i += 1) {
		var newTextNode = tempEl.childNodes[i];
		var oldTextNode = el.childNodes[i];

		// Check if two nodes are the same
		if (!oldTextNode.isEqualNode(newTextNode)) {
			break;
		}
	}
	// If new matching content is shorter then...
	if (i < el.childNodes.length) {
		while (el.childNodes.length > i) {
			el.removeChild(el.lastChild);
		}
	}
	// If new content must be added then...
	if (i < tempEl.childNodes.length) {
		while (i < tempEl.childNodes.length) {
			el.appendChild(document.importNode(tempEl.childNodes[i], true));
			i += 1;
		}
	}
}


function ScrollToBottom() {
	// Add a 50ms delay to make sure the UI has enough time to update, and the scroll ends up at the bottom.
	window.setTimeout(function () {
		window.scroll(0, document.body.scrollHeight);
	}, 50);
}

function Copy() {
	if (isElementFocused())
		document.execCommand("Copy", false, null);
}

function GetSelectedTextAndHtml() {
	var selectedObj = window.getSelection();
	var selectedText = selectedObj.toString();
	var range = selectedObj.getRangeAt(0);
	var fragment = range.cloneContents();
	var div = document.createElement('div');
	div.appendChild(fragment);
	var selectedHtml = div.innerHTML;
	return { text: selectedText, html: selectedHtml };
}

function isElementFocused() {
	return document.activeElement && document.activeElement !== document.body;
}

function FormatDateTime(datetime) {
	var currentDate = new Date();
	var targetDate = new Date(datetime);
	var isToday =
		currentDate.getDate() === targetDate.getDate() &&
		currentDate.getMonth() === targetDate.getMonth() &&
		currentDate.getFullYear() === targetDate.getFullYear();
	return isToday
		? targetDate.toLocaleTimeString([], { timeStyle: 'short' })
		: targetDate.toLocaleString([], { dateStyle: 'short', timeStyle: 'short' });
}

var Item = {};

// Set Item
function SetItem(item) {
	Item = item;
	document.title = item.Name;
}

function GetItemPath(name) {
	var path = Item.Location + "\\" + Item.Name + "\\" + name
	path = isInsideApp
		? path.replace("\\", "/")
		: "file://" + path;
	return path;
}

function SetSettings(settings) {
	if (!settings)
		return;
	var position = settings.ScrollPosition;
	// Check if position is a valid number
	if (isNaN(position))
		return;
	// By default, "null" indicates that the scroll is at the bottom.
	if (position == null)
		ScrollToBottom();
	if (position >= 0)
		SetScrollPosition(position);
}

function GetSettings() {
	var position = GetScrollPosition();
	// Always return null if position is on the bottom.
	if (IsScrollOnTheBottom())
		position = null;
	var settings = {
		"ScrollPosition": position,
	};
	return JSON.stringify(settings);
}

function SetScrollPosition(position) {
	window.scrollTo(0, position);
}

function GetScrollPosition() {
	// Get current scroll position
	var scrollTop = (window.pageYOffset !== undefined) ? window.pageYOffset : (document.documentElement || document.body.parentNode || document.body).scrollTop;
	return scrollTop;
}

function IsScrollOnTheBottom() {
	var scrollTop = Math.max(window.pageYOffset, document.documentElement.scrollTop, document.body.scrollTop);
	var windowHeight = window.innerHeight || document.documentElement.clientHeight || document.getElementsByTagName('body')[0].clientHeight;
	var pageHeight = Math.max(document.documentElement.scrollHeight, document.documentElement.offsetHeight, document.body.scrollHeight, document.body.offsetHeight);
	var delta = Math.ceil(windowHeight + scrollTop);
	// Added Tolerance. experiment with the value
	var tolerance = 50;
	// Check if sum of inner window height and scroll position is within tolerance range of page height
	return Math.abs(pageHeight - delta) <= tolerance;
}

let scrollTimeout;
var keepScrollOnTheBottom = true;

function UpdateKeepScrollOnTheBottom() {
	keepScrollOnTheBottom = IsScrollOnTheBottom();
	console.log("keepScrollOnTheBottom: " + keepScrollOnTheBottom);
}

function window_scroll() {
	// Clear the timeout if it exists
	if (scrollTimeout)
		clearTimeout(scrollTimeout);
	// Set a timeout to run the function after a short delay
	scrollTimeout = setTimeout(function () {
		UpdateKeepScrollOnTheBottom();
		scrollTimeout = null;
	}, 100);
}

window.addEventListener('scroll', window_scroll);

// Keep scrool on the bottom when resizing.
let resizeTimeout;

function window_resize() {
	// Clear the timeout if it exists
	if (resizeTimeout)
		clearTimeout(resizeTimeout);
	// Set a timeout to run the function after a short delay
	resizeTimeout = setTimeout(function () {
		if (keepScrollOnTheBottom)
			ScrollToBottom();
		resizeTimeout = null;
	}, 100);
}

window.addEventListener('scroll', window_scroll);
window.addEventListener('resize', window_resize);

function generateGUID() {
	return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
		var r = Math.random() * 16 | 0,
			v = c === 'x' ? r : (r & 0x3 | 0x8);
		return v.toString(16);
	}).replace(/-/g, "");
}

var ExternalMessageAction = null;

function MessageAction(button, id, action) {
	button.classList.add('clicked');
	setTimeout(function () {
		button.classList.remove('clicked');
	}, 500);
	if (action == 'DataCopy' || action == 'DataApply') {
		// id = "{idPrefix}_{randomGuid}"
		var panel = document.getElementById(id + "_data");
		ExternalMessageAction("", action, panel.innerText);
	} else {
		// id = "{idPrefix}_{messageId}[_{attachmentId}]"
		var messageId = id.substr(idPrefix.length);
		ExternalMessageAction(messageId, action, "");
	}
}

function parseMarkdown(body, boxedCode) {

	// Update rendered to convert language to lowercase for Prism.js.
	var renderer = new marked.Renderer();
	var originalCodeFunction = renderer.code;

	renderer.code = function (code, lang, escaped) {
		lang = lang ? lang.toLowerCase() : undefined;
		// Remove trailing spaces from the code.
		var spacesRx = new RegExp("\\s+$", "g")
		code = code.replace(spacesRx, "");
		// Remove trailing spaces from converted code.
		var html = originalCodeFunction.call(this, code, lang, escaped);
		spacesRx = new RegExp("\\s+</code>", "g")
		html = html.replace(spacesRx, "</code>");
		if (boxedCode) {
			var id = "rnd_" + generateGUID();
			var buttonHTML = document.getElementById("expandableBoxCopyButtonTemplate").innerHTML;
			buttonHTML = buttonHTML
				.replace(/{Id}/g, id);
			var panelHTML = document.getElementById("expandableBoxPanelTemplate").innerHTML;
			panelHTML = panelHTML
				.replace("display: none;", "")
				.replace(/{Id}/g, id)
				.replace(/{Title}/g, lang)
				.replace(/{Instructions}/g, "aaa")
				.replace(/{InstructionsClass}/g, "display-none")
				.replace(/{Data}/g, html)
				.replace(/{DataClass}/g, "")
				.replace(/{TitleButtons}/g, buttonHTML);
			//console.log(panelHTML);
			html = panelHTML;
		}
		return html;
	};

	marked.setOptions({
		// Enable for prism.
		highlight: function (code, lang) {
			return Prism.languages[lang] ? Prism.highlight(code, Prism.languages[lang], lang) : code;
		},
		renderer: renderer
	});
	// Parse the markdown with the modified renderer.
	return marked.parse(body);
}

var isInsideApp = false;

window.onload = function () {
	console.log("window.onload");
	if (
		typeof window.chrome !== 'undefined' &&
		typeof window.chrome.webview !== 'undefined' &&
		typeof window.chrome.webview.hostObjects !== 'undefined' &&
		typeof window.chrome.webview.hostObjects.external !== 'undefined' &&
		typeof window.chrome.webview.hostObjects.external.ExternalMessageAction === 'function'
	) {
		isInsideApp = true;
		ExternalMessageAction = window.chrome.webview.hostObjects.external.ExternalMessageAction;
	} else if (
		typeof window.external !== 'undefined' &&
		typeof window.external.ExternalMessageAction === 'function'
	) {
		isInsideApp = true;
		ExternalMessageAction = window.external.ExternalMessageAction;
	}

	console.log("isInsideApp: " + isInsideApp);
	if (isInsideApp)
		ExternalMessageAction("0", "Loaded", "");

	//SetZoom(50);

	// The code will only run if the file is opened locally.
	if (window.location.href.indexOf("file://") == -1)
		return;

	SimulateMessages();
	SimulateStreaming();
}

//#region Examples

function SimulateMessages() {
	SetItem({
		Location: "d:\\Projects\\Jocys.com GitHub\\VsAiCompanion\\Engine\\Controls\\Chat",
		Name: "ChatListControl"
	});

	var testText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
	var codeText = "" +
		// Add Text code block to test.
		"```Text\r\n" +
		testText + "\r\n" + testText + "\r\n" +
		"```\r\n" +
		"\r\n" +
		// Add C# code block to test.
		"```csharp\r\n" +
		"var x = 1 + y + 0; var x = 1 + y + 0; var x = 1 + y + 0; var x = 1 + y + 0; var x = 1 + y + 0; var x = 1 + y + 0; var x = 1 + y + 0; \r\n" +
		"```\r\n" +
		"\r\n" +
		// Add JavaScript code block to test.
		"```JavaScript\r\n" +
		"var x = 1 + y + 0;\r\n" +
		"```\r\n" +
		"\r\n" +
		// Add CSS code block to test.
		"```css\r\n" +
		"p { color: red }\r\n" +
		"```\r\n";
	for (var i = 0; i < 6; i++) {
		var message = {
			Type: i % 6,
			Id: generateGUID(),
			User: i % 2 == 1 ? "UserIn" : "UserOut",
			Date: new Date().setMinutes(i),
			Body: messageType[i % 6] + "(" + i + ")" + " messsage. ` test <> test ` \r\n" + codeText,
			BodyInstructions: "Instructions included at the start of every message.",
			Data: codeText,
			Attachments: [
				{
					Id: generateGUID(),
					Title: "Data to Process",
					Instructions: "aaa",
					Type: ContextType.Selection,
					Data: "```csharp\r\nvar x = 1 + y;\r\n```",
					IsMarkdown: true,
				},
				{
					Id: generateGUID(),
					Title: "Image",
					Instructions: "",
					Type: ContextType.Image,
					Data: "{ \"Name\": \"test_image.png\", \"Prompt\": \"AI Promtp\", \"Width\": 128, \"Height\": 128}",
					IsMarkdown: false,
				},
				{
					Id: generateGUID(),
					Title: "Audio",
					Instructions: "",
					Type: ContextType.Audio,
					Data: "{ \"Name\": \"test_audio.mp3\", \"Prompt\": \"AI Promtp\"}",
					IsMarkdown: false,
				},
			]
		}
		InsertMessage(message);
	}
	ScrollToBottom();
}

function SimulateStreaming() {
	// Start streaming a new message
	var message = {
		Type: MessageType.In,
		Id: generateGUID(),
		User: "AI Assistant",
		Date: new Date(),
		Body: "Streaming... ",
		Attachments: []
	};

	InsertMessage(message, true);

	// Simulate streaming by appending text over time
	var streamedText = [
		"Hello", " world", "! Here", " is", " some", " streamed", " text.", "\r\n\r\n",
		"Hello", " world", "! Here", " is", " some", " streamed", " text.", "\r\n\r\n",
		"Hello", " world", "! Here", " is", " some", " streamed", " text."
	];
	var index = 0;

	UpdateMessageStatus(message.Id, "Replying");

	function streamNextChunk() {
		if (index < streamedText.length) {
			console.log(streamedText[index]);
			AppendMessageBody(message.Id, streamedText[index]);
			index++;
			setTimeout(streamNextChunk, 100);  // Simulate delay between chunks
		} else {

			// Streaming complete
			delete currentMessageBodies[message.Id];
			UpdateMessageStatus(message.Id, "");
		}
	}
	streamNextChunk();
}


//#endregion


// #region Message Streaming

// Global object to hold the current message bodies during streaming
var currentMessageBodies = {};

/** Append new text to existing message body during streaming.
	@param messageId Unique message Id.
	@param newText New text to append to the message body.
 */
function AppendMessageBody(messageId, newText, autoScroll) {
	console.log("messageId: " + messageId + ", text: " + newText);
	// Get message element by message Id.
	var el = document.getElementById(idPrefix + messageId);
	if (!el) {
		console.log("Error: no element");
		return;
	}
	var bodyEl = el.getElementsByClassName("chat-message-body")[0];
	if (!bodyEl) {
		console.log("Error: no message body element");
		return;
	}
	// Initialize current message body if it doesn't exist
	if (!currentMessageBodies[messageId]) {
		currentMessageBodies[messageId] = "";
	}
	currentMessageBodies[messageId] += newText;
	var currentMessageTextBody = currentMessageBodies[messageId];
	var currentMessageHtmlBody = parseMarkdown(currentMessageTextBody, true);
	ApplyDiffference(bodyEl, currentMessageHtmlBody);
	// Scroll if needed
	if (autoScroll !== true || autoScroll !== false)
		autoScroll = true;
	if (autoScroll && keepScrollOnTheBottom)
		ScrollToBottom();
}


function ApplyDiffference(el, newHtml) {
	var tempEl = el.cloneNode();
	// Convert the new html string to DOM tree
	tempEl.innerHTML = newHtml;
	let i = 0;
	for (; i < tempEl.childNodes.length && i < el.childNodes.length; i += 1) {
		var newTextNode = tempEl.childNodes[i];
		var oldTextNode = el.childNodes[i];

		// Check if two nodes are the same
		if (!oldTextNode.isEqualNode(newTextNode)) {
			break;
		}
	}
	// If new matching content is shorter then...
	if (i < el.childNodes.length) {
		while (el.childNodes.length > i) {
			el.removeChild(el.lastChild);
		}
	}
	// If new content must be added then...
	if (i < tempEl.childNodes.length) {
		while (i < tempEl.childNodes.length) {
			el.appendChild(document.importNode(tempEl.childNodes[i], true));
			i += 1;
		}
	}
	SetVisible(el, !isEmpty(tempEl.innerText));
}

// #endregion
