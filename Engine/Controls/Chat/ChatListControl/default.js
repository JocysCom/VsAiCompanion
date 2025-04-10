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
	console.log(`InsertMessage(message, ${autoScroll})`);
	InsertMessages([message], autoScroll);
}

function InsertMessages(messages, autoScroll) {
	console.log(`InsertMessages(messages, ${autoScroll})`);
	for (var i = 0; i < messages.length; i++) {
		var message = messages[i];
		var chatLog = document.getElementById('chatLog');
		var messageHTML = CreateMessageHtml(message)
		chatLog.insertAdjacentHTML('beforeend', messageHTML);
	}
	UpdateRegenerateButtons();
	// After appending content, render any math expressions
	renderMathExpressions();
	// Scroll to the bottom of the chatLog div.
	if (autoScroll && keepScrollOnTheBottom)
		ScrollToBottom();
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
	// Making items visible can expand content.
	// Make sure to keep scroll on the bottom.
	if (keepScrollOnTheBottom)
		ScrollToBottom();
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
	console.log(`UpdateMessage(message, ${autoScroll})`);
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
	// After appending content, render any math expressions
	renderMathExpressions();
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
				aInstructions = aInstructions + "<br/>";
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
	var messageHTML = createMessageHTML(
		idPrefix + message.Id,
		messageType[message.Type],
		message.IsAutomated ? "Automated" : "Normal",
		FormatDateTime(message.Date),
		bodyContents, data,
		buttons, !isEmpty(bodyContents), !isEmpty(data));
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

/**
 * Generates an expandable panel HTML based on the template
 * @param {string} id - Unique identifier for the panel
 * @param {string} title - Panel title text
 * @param {string} buttonHTML - HTML for buttons in the title bar
 * @param {string} instructions - Instructions content
 * @param {string} codeHTML - Data/code content to display
 * @param {boolean} [showData=true] - Whether to show the data section
 * @param {boolean} [showInstructions=true] - Whether to show the instructions section
 * @param {boolean} [showPanel=true] - Whether to show the entire panel
 * @returns {string} The generated panel HTML
 */
function createExpandableBoxHTML(id, title, buttonHTML, instructions, codeHTML, showData = true, showInstructions = true, showPanel = true) {
	// Create a temporary container to work with the template
	const template = document.getElementById("expandableBoxPanelTemplate");
	const tempContainer = document.createElement('div');
	tempContainer.innerHTML = template.innerHTML;
	// Get the main panel element and set its properties
	const panel = tempContainer.querySelector('.expandable-box');
	panel.id = `${id}_panel`;
	SetVisible(panel, showPanel);
	// Set the title
	const titleElements = tempContainer.querySelectorAll('.expandable-head-text');
	titleElements.forEach(el => {
		el.textContent = title || "";
		el.title = title || "";
	});
	// Set the buttons
	const buttonsElement = tempContainer.querySelector('.expandable-head-buttons');
	buttonsElement.innerHTML = buttonHTML;
	// Set instructions and visibility
	const instructionsElement = tempContainer.querySelector('.expandable-instructions');
	instructionsElement.id = `${id}_instructions`;
	instructionsElement.innerHTML = instructions;
	SetVisible(instructionsElement, showInstructions);
	// Set data content and visibility
	const dataElement = tempContainer.querySelector('.expandable-data');
	dataElement.id = `${id}_data`;
	dataElement.innerHTML = codeHTML;
	SetVisible(dataElement, showData);
	// Return results.
	return tempContainer.innerHTML;
}


/**
 * Generates a chat message HTML based on the template
 * @param {string} id - Unique identifier for the message
 * @param {string} type - Message type (affects styling)
 * @param {boolean} automated - Whether the message is automated
 * @param {string} date - Date/time string for the message
 * @param {string} body - Main message content
 * @param {string} data - Additional data content
 * @param {string} buttons - Additional button HTML
 * @param {boolean} [showBody=true] - Whether to show the message body
 * @param {boolean} [showData=false] - Whether to show the data section
 * @returns {string} The generated message HTML
 */
function createMessageHTML(id, type, automated, date, body, data, buttons, showBody = true, showData = false) {
	// Create a temporary container to work with the template
	const template = document.getElementById("messageTemplate");
	const tempContainer = document.createElement('div');
	tempContainer.innerHTML = template.innerHTML;

	// Replace all placeholders in the HTML except Body and Data
	let html = tempContainer.innerHTML;
	html = html.replace(/{Id}/g, id);
	html = html.replace(/{Type}/g, type);
	html = html.replace(/{Automated}/g, automated);
	html = html.replace(/{Date}/g, date);
	html = html.replace(/{Buttons}/g, buttons || "");

	// Update the temporary container with the partially processed HTML
	tempContainer.innerHTML = html;

	// Handle body and data with DOM manipulation
	const bodyElement = tempContainer.querySelector('.chat-message-body');
	bodyElement.innerHTML = body;
	SetVisible(bodyElement, showBody);

	const dataElement = tempContainer.querySelector('.chat-message-data');
	dataElement.innerHTML = data || "";
	SetVisible(dataElement, showData);

	return tempContainer.innerHTML;
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
	var panelHTML = createExpandableBoxHTML(id, title, titleButtonsHTML, instructions, data, showData, showInstructions, displayPanelContent);
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
	//console.log(`SetVisible(el, ${isVisible})`);
	var isControlVisible = !el.classList.contains("display-none");
	//console.log("SetVisible: " + isVisible + ", isControlVisible: " + isControlVisible);
	// if must show and is not visible then...
	if (isVisible && !isControlVisible) {
		el.classList.remove("display-none");
	}
	// If must hide and is visible then...
	else if (!isVisible && isControlVisible) {
		el.classList.add("display-none");
	}
}

/** Prepare text for markdown and display as HTML. */

/**
 * Prepare text for markdown and display as HTML.
 * This function selectively escapes HTML entities in text that's not within code blocks.
 * @param {string} input - The markdown text to convert
 * @returns {string} The processed text ready for HTML display
 */
function ConvertForDisplayAsHtml(input) {
	if (!input)
		return input;
	var lines = input.split(/\r?\n|\r/);
	// Use a non-backtracking regex approach to detect code block markers
	var rx = new RegExp(/^\s{0,10}[`]{3,10}[a-z0-9\-]{0,30}\s{0,10}$/mi);
	var insideBlock = false;
	for (var i = 0; i < lines.length; i++) {
		var line = lines[i];
		var isMark = rx.test(line);
		if (isMark) {
			// If block ends then...
			if (insideBlock)
				lines[i] = lines[i] + "\r\n<br/>";
			insideBlock = !insideBlock;
			continue;
		}
		// Don't process text inside code blocks
		if (insideBlock)
			continue;

		// Process file references before processing inline code segments
		// This converts file references to special markers
		lines[i] = processFileReferences(lines[i]);

		// Process inline code segments separately
		var parts = lines[i].split('`');
		for (var p = 0; p < parts.length; p++) {
			// Only escape HTML in parts not within inline code (even-indexed parts)
			if (p % 2 === 0) {
				// Use a safer approach that avoids double-encoding
				parts[p] = safeEncodeHtml(parts[p]);
			}
		}
		// Add extra new line for markdown to add `<br/>` correctly.
		lines[i] = parts.join('`') + "\r\n";
	}
	var processedText = lines.join("\r\n");
	return processedText;
}

/**
 * Safely encodes HTML entities while avoiding double-encoding
 * @param {string} text - The text to encode
 * @returns {string} The safely encoded text
 */
function safeEncodeHtml(text) {
	// Create a temporary element
	var el = document.createElement('div');
	// Set the text content (browser handles the encoding)
	el.textContent = text;
	// Get the HTML as a string (with entities properly encoded)
	var encodedText = el.innerHTML;
	// Clean up
	el = null;
	return encodedText;
}

// The original EscapeHtml function can remain for other uses
function EscapeHtml(unsafe) {
	return unsafe
		.replace(/<think>/g, "##THINK_START##")
		.replace(/<\/think>/g, "##THINK_END##")
		.replace(/</g, "&lt;")
		.replace(/>/g, "&gt;")
		.replace(/##THINK_START##/g, "<think>")
		.replace(/##THINK_END##/g, "</think>")
		.replace(/&/g, "&amp;")
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
	// Render math expressions after updating content
	renderMathExpressions();
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
	console.log(`SetSettings(${settings})`);
	if (!settings)
		return;
	var position = settings.ScrollPosition;
	console.log(`settings.ScrollPosition = ${position})`);
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
	// Always return null if position is on the bottom.
	var position = IsScrollOnTheBottom()
		? null
		: Math.ceil(GetScrollPosition());
	var settings = {
		"ScrollPosition": position,
	};
	return settings;
}

function SetScrollPosition(position) {
	console.log(`SetScrollPosition(${position})`);
	var position = (position)
		? position
		: document.body.scrollHeight;
	// Add a 50ms delay to make sure the UI has enough time to update, and the scroll ends up at the bottom.
	window.setTimeout(function () {
		window.scrollTo(0, position);
	});
}

function ScrollToBottom() {
	SetScrollPosition(null)
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
	console.log(`window_resize`);
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

/**
 * Parses markdown text and converts it to HTML with syntax highlighting and math support
 * @param {string} body - The markdown text to parse
 * @param {boolean} [boxedCode=false] - Whether to place code blocks in expandable boxes
 * @returns {string} The HTML representation of the markdown
 */
function parseMarkdown(body, boxedCode = false) {
	if (!body) return '';

	// Process <think> blocks first
	body = body.replace(/^(<think>)([\s\S]*?)(<\/think>|$)/gi, function (match, start, content, end) {
		// Replace 2+ newlines with a line break tag
		var cleaned = content.replace(/(?:[ \t]*\r?\n){2,}/g, "<br/>\r\n");
		return start + cleaned + end;
	});

	// Remove trailing spaces
	body = body.trim();

	// Define language fallbacks in a more compact format
	// Format: "targetLanguage": ["sourceLanguage1", "sourceLanguage2", ...]
	const languageFallbackMap = {
		"css": ["css-extras", "css-extra"],
		"html": ["htm", "xhtml", "aspx", "asp"],
		"xml": ["xaml", "axml", "svg"],
		"javascript": ["js", "jsx"],
		"typescript": ["ts", "tsx"],
		"cs": ["csharp", "c#"],
		"python": ["py"],
		"powershell": ["ps", "ps1"],
		"bash": ["shell"],
		"batch": ["bat", "cmd"]
	};

	// Create a flattened lookup map for faster access
	const languageFallbacks = {};
	for (const [targetLang, sourceLangs] of Object.entries(languageFallbackMap)) {
		for (const sourceLang of sourceLangs) {
			languageFallbacks[sourceLang] = targetLang;
		}
	}

	// Create a custom renderer
	const renderer = new marked.Renderer();

	// Override code rendering function
	renderer.code = function (code, infostring, escaped) {
		// Normalize code content
		let codeText = '';
		if (typeof code === 'string') {
			codeText = code;
		} else if (code && typeof code.text === 'string') {
			codeText = code.text;
		} else if (code && typeof code.raw === 'string') {
			const match = /^```.*?\n([\s\S]*?)```$/m.exec(code.raw);
			codeText = match ? match[1] : code.raw;
		} else {
			codeText = String(code || '');
		}

		// Clean the code
		const cleanedCode = codeText.trim();

		// Normalize language identification
		let language = '';
		if (infostring) {
			language = infostring.toLowerCase();
		} else if (code && code.lang) {
			language = code.lang.toLowerCase();
		}

		// Debug logging
		//console.log(`Processing code block with language: "${language}"`);

		// Determine highlighting approach
		let highlightedCode = '';
		let effectiveLanguage = language;

		// Try highlighting with the specified language
		if (language && Prism.languages[language]) {
			try {
				highlightedCode = Prism.highlight(cleanedCode, Prism.languages[language], language);
				//console.log(`Highlighted with language: ${language}`);
			} catch (e) {
				console.error(`Error highlighting with ${language}:`, e);
				highlightedCode = EscapeHtml(cleanedCode);
			}
		}
		// Try fallback language if defined
		else if (language && languageFallbacks[language] && Prism.languages[languageFallbacks[language]]) {
			const fallbackLang = languageFallbacks[language];
			try {
				highlightedCode = Prism.highlight(cleanedCode, Prism.languages[fallbackLang], fallbackLang);
				console.log(`Used fallback ${fallbackLang} for ${language}`);
				effectiveLanguage = language; // Keep original language for display
			} catch (e) {
				console.error(`Error with fallback ${fallbackLang} for ${language}:`, e);
				highlightedCode = EscapeHtml(cleanedCode);
			}
		}
		// Default to plain text
		else {
			console.log(`No syntax highlighting for language: ${language}`);
			highlightedCode = EscapeHtml(cleanedCode);
			effectiveLanguage = language || 'text';
		}

		// Create HTML for the code block - always include language class if available
		let codeHTML = `<pre><code class="language-${effectiveLanguage || 'text'}">`;
		codeHTML += highlightedCode;
		codeHTML += '</code></pre>';

		// If boxed code is requested, wrap in expandable box
		if (boxedCode) {
			const id = "rnd_" + generateGUID();
			const buttonHTML = document.getElementById("expandableBoxCopyButtonTemplate").innerHTML
				.replace(/{Id}/g, id);

			// Make sure to display the language in the title
			const displayLanguage = effectiveLanguage || 'text';

			return createExpandableBoxHTML(id, displayLanguage, buttonHTML, "", codeHTML);
		}

		return codeHTML;
	};

	// Set marked options
	marked.setOptions({
		renderer: renderer,
		highlight: null, // Disable built-in highlighting
		gfm: true,
		breaks: true,
		pedantic: false,
		xhtml: false,
		smartLists: true,
		smartypants: false
	});

	// Parse the markdown and return the HTML
	try {
		// First pass: standard markdown parsing
		let html = marked.parse(body);

		// Replace file reference markers with actual links
		html = replaceFileReferenceMarkers(html);

		// Second pass: process math expressions for KaTeX
		html = processMathExpressions(html);

		return html;
	} catch (e) {
		console.error("Markdown parsing error:", e);
		return "<p>" + body.replace(/\n/g, "<br/>") + "</p>";
	}
}

/**
 * Processes math expressions in HTML for KaTeX rendering
 * @param {string} html - The HTML to process
 * @returns {string} HTML with math expressions prepared for KaTeX
 */
function processMathExpressions(html) {
	// Process display math expressions ($$...$$)
	html = html.replace(/\$\$([\s\S]+?)\$\$/g, function (match, expression) {
		const escapedExpression = EscapeHtml(expression.trim());
		return `<div class="math-display" data-math="${escapedExpression}">$$${expression}$$</div>`;
	});

	// Process inline math expressions ($...$) - skip currency-like expressions
	html = html.replace(/\$([^\$\n]+?)\$/g, function (match, expression) {
		// Skip if it looks like currency
		if (/^\s*\d+([,.]\d+)?\s*$/.test(expression)) {
			return match;
		}
		const escapedExpression = EscapeHtml(expression.trim());
		return `<span class="math-inline" data-math="${escapedExpression}">$${expression}$</span>`;
	});

	return html;
}

// Separate debugging function that doesn't reference external variables
function debugMarkedTokens() {
	console.log("Running marked debug...");

	// Create a test case
	const testMarkdown = "```javascript\nconst x = 1;\n```";

	// Create a new renderer for debugging
	const debugRenderer = new marked.Renderer();

	// Override the code method for debugging
	debugRenderer.code = function (code, infostring, escaped) {
		console.log("Debug - Code block received:");
		console.log("Type:", typeof code);
		console.log("Value:", code);
		if (typeof code === 'object') {
			console.log("Properties:", Object.keys(code));
		}
		console.log("Infostring:", infostring);
		console.log("Escaped:", escaped);

		// Return simple HTML
		return '<pre><code>' + (typeof code === 'string' ? code : JSON.stringify(code)) + '</code></pre>';
	};

	// Parse with debug renderer
	const originalOptions = marked.getDefaults();
	marked.setOptions({ renderer: debugRenderer });
	marked.parse(testMarkdown);

	// Restore original options
	marked.setOptions(originalOptions);

	console.log("Debug complete");
}

var isInsideApp = false;

window.addEventListener('load', function () {
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
});

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
		"```\r\n" +
		"\r\n" +
		// Add HTML/XML code block to test.
		"```html\r\n" +
		"<body><span>Lorem ipsum dolor sit amet.</span></body>\r\n" +
		"```\r\n" +
		"\r\n" +
		// Add HTML/XML code block to test.
		"```xaml\r\n" +
		"<body><span>Lorem ipsum dolor sit amet.</span></body>\r\n" +
		"```\r\n" +
		"\r\n" +
		// Add mathematical expression.
		"**The Cauchy-Schwarz Inequality**\r\n" +
		"$$\\left(\\sum_{ k=1 } ^ n a_k b_k \\right) ^ 2 \\leq \\left(\\sum_{ k=1 } ^ n a_k ^ 2 \\right) \\left(\\sum_{ k=1 } ^ n b_k ^ 2 \\right)$$\r\n" +
		"\r\n";

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
		"<think>",
		"I", " am", " thinking", ".", " Here", " is", " some", " streamed", " text. ",
		"I", " am", " thinking", ".", " Here", " is", " some", " streamed", " text. ", "\r\n\r\n",
		"I", " am", " thinking", ".", " Here", " is", " some", " streamed", " text. ",
		"</think>",
		"Hello", " world", "!", " Here", " is", " some", " streamed", " text.", "\r\n\r\n",
		"<think>", "Hello", " world", "!", "</think>", " Here", " is", " some", " streamed", " text.", "\r\n\r\n",
		"$$\\left(\\sum_{ k=1 } ^ n a_k b_k \\right) ^ 2 \\leq \\left(\\sum_{ k=1 } ^ n a_k ^ 2 \\right) \\left(\\sum_{ k=1 } ^ n b_k ^ 2 \\right)$$\r\n"
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
	// After appending content, render any math expressions
	renderMathExpressions();
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

	// Making items visible can expand content.
	// Make sure to keep scroll on the bottom.
	if (keepScrollOnTheBottom)
		ScrollToBottom();
}

// #endregion

//#region KaText

/**
 * Renders all math expressions on the page using KaTeX
 * @param {boolean} [retryOnFailure=true] - Whether to retry if KaTeX isn't loaded yet
 */
function renderMathExpressions(retryOnFailure = true) {
	if (typeof katex === 'undefined') {
		console.warn('KaTeX library not loaded yet. Math expressions will not be rendered.');
		if (retryOnFailure) {
			// Retry after a short delay
			setTimeout(() => renderMathExpressions(false), 500);
		}
		return;
	}

	// Find all unrendered math elements
	const mathElements = document.querySelectorAll('.math-inline:not(.katex-rendered), .math-display:not(.katex-rendered)');

	if (mathElements.length > 0) {
		console.log(`Rendering ${mathElements.length} math expressions`);
	}

	mathElements.forEach(element => {
		// Skip if element is inside a code block
		if (isElementInCodeBlock(element)) {
			// Mark as processed to avoid future attempts
			element.classList.add('katex-rendered');
			element.classList.add('in-code-block');
			return;
		}

		try {
			const expression = element.getAttribute('data-math');
			const isDisplay = element.classList.contains('math-display');

			katex.render(expression, element, {
				throwOnError: false,
				displayMode: isDisplay,
				strict: "ignore"  // Use "ignore" to suppress warnings
			});

			// Mark as rendered to avoid re-processing
			element.classList.add('katex-rendered');
		} catch (e) {
			console.error('KaTeX rendering error:', e);
			// Keep the original format if rendering fails
			const mathDelimiter = element.classList.contains('math-display') ? '$$' : '$';
			element.textContent = mathDelimiter + element.getAttribute('data-math') + mathDelimiter;
		}
	});

	// Call layout fix after rendering
	fixLayoutIssues();
}

/**
 * Checks if an element is inside a code block
 * @param {Element} element - The element to check
 * @returns {boolean} - True if the element is inside a code block
 */
function isElementInCodeBlock(element) {
	let current = element;
	while (current) {
		// Check if element is inside a code tag, pre tag, or has a language class
		if (current.tagName === 'CODE' ||
			current.tagName === 'PRE' ||
			(current.className &&
				(current.className.includes('language-') ||
					current.className.includes('expandable-data')))) {
			return true;
		}

		// Also check parent element's class for code-related classes
		if (current.parentElement &&
			current.parentElement.className &&
			(current.parentElement.className.includes('language-') ||
				current.parentElement.className.includes('expandable-data'))) {
			return true;
		}

		current = current.parentElement;
	}
	return false;
}

// Add robust event listeners for initialization
window.addEventListener('DOMContentLoaded', function () {
	// Wait a moment for everything to initialize
	setTimeout(debugMarkedTokens, 1000);

	console.log("DOM content loaded, initializing KaTeX support");

	// Function to check if KaTeX is loaded and render math
	function checkKatexAndRender() {
		if (typeof katex !== 'undefined') {
			console.log("KaTeX is loaded, rendering math expressions");
			renderMathExpressions(false);
			return true;
		}
		return false;
	}

	// Try immediately
	if (!checkKatexAndRender()) {
		// If not loaded, set up a polling mechanism
		console.log("KaTeX not immediately available, setting up polling");
		let attempts = 0;
		const maxAttempts = 50; // 5 seconds max

		const katexCheckInterval = setInterval(function () {
			attempts++;
			if (checkKatexAndRender() || attempts >= maxAttempts) {
				clearInterval(katexCheckInterval);
				if (attempts >= maxAttempts) {
					console.error("Failed to load KaTeX after multiple attempts");
				}
			}
		}, 100);
	}

	// Add layout fix handler
	window.addEventListener('resize', fixLayoutIssues);
	fixLayoutIssues();
});

/**
 * Fixes any layout issues and excessive space
 */
function fixLayoutIssues() {
	// Fix excessive space at the bottom
	const chatLog = document.getElementById('chatLog');
	const body = document.body;

	// Reset any extreme height values
	if (chatLog.style.height === 'auto') {
		chatLog.style.height = '';
	}

	// Clean up any inline styles that might be causing issues
	if (body.scrollHeight > window.innerHeight * 2) {
		//console.log("Detected potential excessive space, applying fix");
		body.style.paddingBottom = '0';
		chatLog.style.marginBottom = '0';
	}
}

//#endregion

//#region References

/**
 * Processes file references in the format #file:'<file_path>' and converts them to special markers
 * that will be replaced with actual links after HTML escaping
 * @param {string} text - The text to process
 * @returns {string} - The processed text with file references converted to markers
 */
function processFileReferences(text) {
	if (!text)
		return text;
	// Regex to match #file:'<file_path>' pattern
	const fileRefRegex = /#file:'([^']+)'/g;
	return text.replace(fileRefRegex, function (match, filePath) {
		// Create a unique marker with a random ID
		const markerId = "FILE_REF_" + Math.random().toString(36).substring(2, 15);
		// Store the file path in a global object for later retrieval
		if (!window.fileRefMarkers) {
			window.fileRefMarkers = {};
		}
		window.fileRefMarkers[markerId] = filePath;
		// Return a marker that won't be affected by HTML escaping
		return "[[" + markerId + "]]";
	});
}

/**
 * Replaces file reference markers with actual HTML links
 * @param {string} html - The HTML with file reference markers
 * @returns {string} - The HTML with actual file reference links
 */
function replaceFileReferenceMarkers(html) {
	if (!html || !window.fileRefMarkers) return html;

	// Regex to match our markers
	const markerRegex = /\[\[(FILE_REF_[a-z0-9]+)\]\]/g;

	return html.replace(markerRegex, function (match, markerId) {
		const filePath = window.fileRefMarkers[markerId];
		if (!filePath) return match;
		// Convert the path to a proper file:/// URL
		let fileUrl = filePath;
		// If the path doesn't already have a protocol
		if (!fileUrl.match(/^[a-z]+:\/\//i)) {
			// Normalize path separators for URLs (use forward slashes)
			fileUrl = fileUrl.replace(/\\/g, '/');
			// Add the appropriate file:/// prefix based on path format
			if (fileUrl.match(/^[a-zA-Z]:\//)) {
				// Windows path like C:/folder - needs three slashes
				fileUrl = 'file:///' + fileUrl;
			} else if (fileUrl.startsWith('/')) {
				// Unix absolute path starting with / - needs file://
				fileUrl = 'file://' + fileUrl;
			} else {
				// Relative path - make it absolute with three slashes
				fileUrl = 'file:///' + fileUrl;
			}
		}
		// Create a standard anchor tag with the file:/// URL
		return `<a href="${fileUrl}" class="file-reference" target="_blank">${filePath}</a>`;
	});
}

//#endregion

