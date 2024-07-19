use std::sync::atomic::{AtomicBool, Ordering};

const REFRESH_TIME_SEC: u64 = 3;

use models::{ChatMessage, NewMessageRequest};
use requests::{get_messages, send_message};
use wasm_bindgen::JsCast;
use web_sys::{FormData, HtmlFormElement, HtmlInputElement};
use yew::prelude::*;

pub enum Msg {
    Nothing,
    SetFetchState(Result<Vec<ChatMessage>, ErrorMessage>),
    GetMessages,
    SubmitMessage(Result<NewMessageRequest, String>),
    ToggleRefresh,
}

pub enum RootMsg {
    SetChildCallback(Callback<Msg>),
}

pub struct State {
    pub refresh_enabled: bool,
}

pub struct ChatBoxComponent {
    state: State,
    messages: Vec<ChatMessage>,
    fetch_error: Option<ErrorMessage>,
    fetch_queued: AtomicBool,
}

#[derive(Clone)]
pub struct ErrorMessage {
    description: String,
    details: Option<String>,
}

#[derive(Properties, PartialEq)]
pub struct Props {
    pub on_new_msg: Callback<Result<NewMessageRequest, String>>,
}

pub struct App {
    cb: Option<Callback<Msg>>,
}

impl Component for App {
    type Message = RootMsg;

    type Properties = ();

    fn create(_ctx: &Context<Self>) -> Self {
        App { cb: None }
    }

    fn update(&mut self, _ctx: &Context<Self>, msg: Self::Message) -> bool {
        match msg {
            RootMsg::SetChildCallback(cb) => self.cb = Some(cb),
        }
        true
    }

    fn view(&self, _ctx: &Context<Self>) -> Html {
        let cb = self.cb.clone();
        html! {
            <>
            <div class="content">
                <h1>{ "XIV Chat Bridge" }</h1>
                <div class="chatBox">
                    <ChatBoxComponent />
                </div>
                <div class="chatInput">
                    <form enctype={ "multipart/form-data" } onsubmit={move |e: SubmitEvent| {
                            e.prevent_default();
                            if let Some(cb) = cb.as_ref() {
                                let form = e
                                    .target()
                                    .and_then(|t| t.dyn_into::<HtmlFormElement>().ok());
                                let res = if let Some(form) = form {
                                    let data = FormData::new_with_form(&form)
                                        .map_err(|e| e.as_string().unwrap_or_default())
                                        .and_then(|form_data| form_data.try_into());
                                    if let Some(input) = web_sys::window()
                                        .and_then(|w| w.document())
                                        .and_then(|doc| doc.get_element_by_id("text"))
                                        .and_then(|elem| elem.dyn_into::<HtmlInputElement>().ok()) {
                                            input.set_value("");
                                        }
                                    data
                                } else {
                                    Err("unable to get form reference".to_owned())
                                };
                                cb.emit(Msg::SubmitMessage(res));
                            }
                        }}>
                        <label for="chatType">{ "ChatType:" }</label>
                        <select name="chatType" id="chatType">
                            <option value="Say" selected=true >{ "Say" }</option>
                            <option value="Shout">{ "Shout" }</option>
                            <option value="Yell">{ "Yell" }</option>
                            <option value="Party">{ "Party" }</option>
                            <option value="FC">{ "FC" }</option>
                        </select>
                        <label for="text">{ "Message:" }</label>
                        <input type="text" id="text" name="text" />
                        <button type="submit">{ "Send" }</button>
                    </form>
                </div>
            </div>
            <footer>
                { "Made by Elena" }
            </footer>
            </>
        }
    }
}

impl Component for ChatBoxComponent {
    type Message = Msg;
    type Properties = ();

    fn create(ctx: &Context<Self>) -> Self {
        let cb = ctx.link().callback(|msg| msg);

        let parent_link = ctx
            .link()
            .get_parent()
            .expect("this needs to not be orphaned..");
        let parent = parent_link.downcast::<App>();
        parent.send_message(RootMsg::SetChildCallback(cb));
        ctx.link().send_message(Msg::GetMessages);
        ChatBoxComponent {
            state: State {
                refresh_enabled: true,
            },
            messages: Vec::new(),
            fetch_error: None,
            fetch_queued: AtomicBool::new(true),
        }
    }

    fn update(&mut self, ctx: &Context<Self>, msg: Self::Message) -> bool {
        match msg {
            Msg::Nothing => false,
            Msg::ToggleRefresh => {
                self.state.refresh_enabled = !self.state.refresh_enabled;
                if self.state.refresh_enabled && !self.fetch_queued.load(Ordering::Relaxed) {
                    self.fetch_queued.store(true, Ordering::Relaxed);
                    ctx.link().send_future(async move {
                        wasmtimer::tokio::sleep(std::time::Duration::from_secs(REFRESH_TIME_SEC))
                            .await;
                        Msg::GetMessages
                    });
                }
                false
            }
            Msg::SetFetchState(state) => {
                match state {
                    Ok(messages) => {
                        self.fetch_error = None;
                        self.messages = messages
                    }
                    Err(e) => self.fetch_error = Some(e),
                }

                if self.state.refresh_enabled && !self.fetch_queued.load(Ordering::Relaxed) {
                    self.fetch_queued.store(true, Ordering::Relaxed);
                    ctx.link().send_future(async move {
                        wasmtimer::tokio::sleep(std::time::Duration::from_secs(REFRESH_TIME_SEC))
                            .await;
                        Msg::GetMessages
                    });
                }
                true
            }
            Msg::GetMessages => {
                self.fetch_queued.store(false, Ordering::Relaxed);
                ctx.link()
                    .send_future(async { Msg::SetFetchState(get_messages().await) });
                false
            }
            Msg::SubmitMessage(msg) => {
                match msg {
                    Ok(msg) => {
                        let msg = msg.clone();
                        ctx.link().send_future(async move {
                            match send_message(&msg).await {
                                Ok(_) => {
                                    wasmtimer::tokio::sleep(std::time::Duration::from_secs(1))
                                        .await;
                                    Msg::GetMessages
                                }
                                Err(e) => Msg::SetFetchState(Err(e)),
                            }
                        });
                    }
                    Err(e) => log::error!("{e}"),
                }
                false
            }
        }
    }

    fn view(&self, ctx: &Context<Self>) -> Html {
        html! {
        <>
        if let Some(err) = self.fetch_error.clone() {
            <div class="errorReport">
                <span class="errorMessage">{ err.description.clone() }</span>
                <button type="button" title="Copy details" onclick={ctx.link().callback(move |_| {
                    if let Some(details) = &err.details {
                        if let Some(clipboard) = web_sys::window()
                            .and_then(|w| w.navigator().clipboard()) {
                                let _ = clipboard.write_text(details);
                            }
                        }
                        Msg::Nothing
                    }
                )

                }>{ "📄" }</button>
            </div>
        }
        <div class="chatBoxHeader">
            <div class="manualRefresh">
                <button type="button" onclick={ctx.link().callback(|_| Msg::GetMessages)} style="width: fit-content; align-self: center;">
                            { "Refresh" }
                            </button>
            </div>
            <div class="refreshSettings">
                <input type="checkbox" id="refresh" name="refresh" checked={self.state.refresh_enabled} onclick={ctx.link().callback(|_| Msg::ToggleRefresh) }/>
                <label for="refresh">{ "Auto Refresh" }</label>
            </div>
        </div>
        <div class="chatBoxContent" id="chatBoxContent">
            {
                html! {
                    <>
                    {
                        self.messages.iter()
                            .rev()
                            .map(|msg| {
                                let color = msg.chat_type.get_color();
                                let uniq = format!("{}_{}", msg.timestamp, msg.sender_name);
                                // todo: wrap message to next line. probably have div as float with wrapping and text set to fit content or smth?
                                html!{
                                    <>
                                        <div key={uniq} class="chatEntry">
                                            <div class="timestamp"> { format!("[{}]", msg.formatted_timestamp()) } </div>
                                            <div class="chatType" style= { format!("color: {color}") }> { format!("[{}]", msg.chat_type) } </div>
                                            if !msg.sender_name.is_empty() { <div class="sender" style= { format!("color: {color}") }> { format!("{}:", msg.sender_name) } </div> }
                                            <span class="chatMessage" style= { format!("color: {color}") }>{ format!("{text}", text = msg.text) } </span>
                                        </div>
                                    </>
                                }
                            })
                            .collect::<Html>()
                    }
                    </>
                }
            }
        </div>
        </>
        }
    }
}

pub mod models {
    use std::fmt::Display;

    use serde::{Deserialize, Serialize};
    use web_sys::FormData;

    #[derive(Clone, Debug, Deserialize, Serialize, PartialEq, PartialOrd, Hash)]
    #[serde(rename_all = "camelCase")]
    #[repr(u32)]
    pub enum ChatType {
        None,
        Debug,
        Urgent,
        Notice,
        Say,
        Shout,
        Yell,
        // syntax: /tell player name@world message
        TellOutgoing,
        TellIncoming,
        Party,
        FreeCompany,
        Alliance,
        CrossParty,
        #[serde(rename = "ls1")]
        LinkShell1,
        #[serde(rename = "ls2")]
        LinkShell2,
        #[serde(rename = "ls3")]
        LinkShell3,
        #[serde(rename = "ls4")]
        LinkShell4,
        #[serde(rename = "ls5")]
        LinkShell5,
        #[serde(rename = "ls6")]
        LinkShell6,
        #[serde(rename = "ls7")]
        LinkShell7,
        #[serde(rename = "ls8")]
        LinkShell8,
        CrossLinkShell1,
        CrossLinkShell2,
        CrossLinkShell3,
        CrossLinkShell4,
        CrossLinkShell5,
        CrossLinkShell6,
        CrossLinkShell7,
        CrossLinkShell8,
        NoviceNetwork,
        CustomEmote,
        StandardEmote,
        Echo,
        SystemError,
        SystemMessage,
        ErrorMessage,
        GatheringSystemMessage,
        #[serde(rename = "npcDialogue")]
        NPCDialogue,
        #[serde(rename = "npcDialogueAnnouncements")]
        NPCDialogueAnnouncements,
        RetainerSale,
        #[serde(untagged)]
        Unimplemented(String),
    }

    #[derive(Deserialize, Hash)]
    pub struct ChatMessage {
        pub timestamp: String,
        #[serde(rename = "type")]
        pub chat_type: ChatType,
        #[serde(rename = "senderName")]
        pub sender_name: String,
        pub text: String,
    }

    #[derive(Clone, Serialize)]
    pub struct NewMessageRequest {
        #[serde(rename = "type")]
        pub chat_type: ChatType,
        pub text: String,
    }

    impl TryFrom<FormData> for NewMessageRequest {
        type Error = String;

        fn try_from(value: FormData) -> Result<Self, Self::Error> {
            let chat_type: ChatType = value
                .get("chatType")
                .as_string()
                .ok_or_else(|| "Unable to get property for chatType".to_owned())
                .and_then(|s| s.try_into())?;
            let text = value
                .get("text")
                .as_string()
                .ok_or_else(|| "Unable to get property for text".to_owned())?;
            let me = Self { chat_type, text };

            Ok(me)
        }
    }

    impl TryFrom<String> for ChatType {
        type Error = String;

        fn try_from(value: String) -> Result<Self, Self::Error> {
            Ok(match value.as_str() {
                "Say" => ChatType::Say,
                "Shout" => ChatType::Shout,
                "Yell" => ChatType::Yell,
                "Party" => ChatType::Party,
                "FC" => ChatType::FreeCompany,
                _ => return Err("Unsupported chatType".to_owned()),
            })
        }
    }

    impl Display for ChatType {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            let str = match &self {
                ChatType::None => "",
                ChatType::Debug => "DBG",
                ChatType::Urgent => "Urgent",
                ChatType::Notice => "Notice",
                ChatType::Say => "Say",
                ChatType::Shout => "Shout",
                ChatType::Yell => "Yell",
                ChatType::TellOutgoing => "Tell>",
                ChatType::TellIncoming => "Tell<",
                ChatType::Party => "Party",
                ChatType::FreeCompany => "FC",
                ChatType::Alliance => "Alliance",
                ChatType::CrossParty => "Party",
                ChatType::LinkShell1 => "LS1",
                ChatType::LinkShell2 => "LS2",
                ChatType::LinkShell3 => "LS3",
                ChatType::LinkShell4 => "LS4",
                ChatType::LinkShell5 => "LS5",
                ChatType::LinkShell6 => "LS6",
                ChatType::LinkShell7 => "LS7",
                ChatType::LinkShell8 => "LS8",
                ChatType::CrossLinkShell1 => "CWLS1",
                ChatType::CrossLinkShell2 => "CWLS2",
                ChatType::CrossLinkShell3 => "CWLS3",
                ChatType::CrossLinkShell4 => "CWLS4",
                ChatType::CrossLinkShell5 => "CWLS5",
                ChatType::CrossLinkShell6 => "CWLS6",
                ChatType::CrossLinkShell7 => "CWLS7",
                ChatType::CrossLinkShell8 => "CWLS8",
                ChatType::NoviceNetwork => "NN",
                ChatType::CustomEmote => "CEmote",
                ChatType::StandardEmote => "Emote",
                ChatType::Echo => "Echo",
                ChatType::SystemError => "Error(System)",
                ChatType::SystemMessage => "System",
                ChatType::ErrorMessage => "Error",
                ChatType::GatheringSystemMessage => "Gathering",
                ChatType::NPCDialogue | ChatType::NPCDialogueAnnouncements => "NPC",
                ChatType::RetainerSale => "Retainer",
                ChatType::Unimplemented(val) => {
                    log::warn!("Unknown chatType: {val}");
                    "Unknown"
                }
            };
            write!(f, "{str}",)
        }
    }

    impl ChatType {
        pub fn get_color(&self) -> String {
            match self {
                Self::Yell => "#fcfc03",
                Self::Shout => "#ffce63",
                Self::TellIncoming | Self::TellOutgoing => "#f263ff",
                Self::Alliance => "#ed9511",
                Self::FreeCompany => "#4ef542",
                Self::Party => "#426ff5",
                cwl if (Self::CrossLinkShell1..=Self::CrossLinkShell8).contains(cwl) => "#9f3cbd",
                Self::NoviceNetwork => "#cfe05c",
                ls if (Self::LinkShell1..=Self::LinkShell8).contains(ls) => "#fad2b9",
                Self::StandardEmote | Self::CustomEmote => "#e1faf9",
                Self::NPCDialogue | Self::NPCDialogueAnnouncements => "#6ead10",
                _ => "#FFFFFFFF",
            }
            .to_owned()
        }
    }

    impl ChatMessage {
        pub fn formatted_timestamp(&self) -> String {
            if let Ok(date_time) = self.timestamp.parse::<chrono::DateTime<chrono::Utc>>() {
                chrono::DateTime::<chrono::Local>::from(date_time)
                    .format("%Y-%m-%d %H:%M")
                    .to_string()
            } else {
                "N/A".to_owned()
            }
        }
    }
}

pub mod requests {
    use std::error::Error;

    use serde::de;

    use super::{
        models::{ChatMessage, NewMessageRequest},
        ErrorMessage,
    };

    lazy_static::lazy_static!(
        static ref CLIENT: reqwest_wasm::Client = {
            let mut headers = reqwest_wasm::header::HeaderMap::new();
            headers.insert(
                reqwest_wasm::header::ACCESS_CONTROL_ALLOW_ORIGIN,
                reqwest_wasm::header::HeaderValue::from_static("*"),
            );
            reqwest_wasm::ClientBuilder::new()
                .default_headers(headers)
                .build()
                .expect("should not happen o.O")
        };
    );

    const FALLBACK_URL: &str = "http://localhost:9876";
    const MESSAGES_URI: &str = "/messages";

    pub async fn get_messages() -> Result<Vec<ChatMessage>, ErrorMessage> {
        let response = CLIENT
            .clone()
            .get(url(MESSAGES_URI)?)
            .send()
            .await
            .map_err(|e| {
                log::error!(
                    "{:?} failed. Caused By: {}",
                    e.url(),
                    e.source().map_or_else(|| String::new(), |s| s.to_string())
                );
                ErrorMessage {
                    description: "Unable to get messages from Server. Make sure it is running!"
                        .to_owned(),
                    details: e.source().map(|source| source.to_string()),
                }
            })?;
        from_response::<Vec<ChatMessage>>(response).await
    }

    pub async fn send_message(msg: &NewMessageRequest) -> Result<(), ErrorMessage> {
        let response = CLIENT
            .clone()
            .post(url(MESSAGES_URI)?)
            .json(msg)
            .send()
            .await
            .map_err(|e| {
                log::error!(
                    "{:?} failed. Caused By: {}",
                    e.url(),
                    e.source().map_or_else(|| String::new(), |s| s.to_string())
                );
                ErrorMessage {
                    description: "Unable to send message to Server. Make sure it is running!"
                        .to_owned(),
                    details: e.source().map(|source| source.to_string()),
                }
            })?;

        match response.status().as_u16() {
            200..=299 => Ok(()),
            400 => Err(ErrorMessage {
                description: format!(
                    "Bad request: {}",
                    response.text().await.unwrap_or_else(|_| String::new())
                ),
                details: None,
            }),
            unknown_code => Err(ErrorMessage {
                description: format!("unexpected response: {}", unknown_code),
                details: None,
            }),
        }
    }

    async fn from_response<T>(value: reqwest_wasm::Response) -> Result<T, ErrorMessage>
    where
        T: de::DeserializeOwned,
    {
        match value.status().as_u16() {
            200..=299 => {}
            _ => {
                return Err(ErrorMessage {
                    description: format!("HTTP Error: {error_code}", error_code = value.status()),
                    details: value.text().await.ok(),
                })
            }
        }

        let bytes = value.text().await.map_err(|e| ErrorMessage {
            description: "Unable to read response".to_owned(),
            details: Some(e.to_string()),
        })?;

        serde_json::from_str(&bytes).map_err(|e| ErrorMessage {
            description: "JSON parsing failed".to_owned(),
            details: Some(e.to_string()),
        })
    }

    #[cfg(feature = "devtest")]
    fn url(uri: &str) -> Result<reqwest_wasm::Url, ErrorMessage> {
        let url = format!("{FALLBACK_URL}{uri}");
        reqwest_wasm::Url::parse(&url).map_err(|e| ErrorMessage {
            description: "Unable to parse URL".to_owned(),
            details: Some(e.to_string()),
        })
    }

    #[cfg(not(feature = "devtest"))]
    fn url(uri: &str) -> Result<reqwest_wasm::Url, ErrorMessage> {
        let url = format!(
            "{}{uri}",
            web_sys::window()
                .and_then(|w| w.document())
                .and_then(|doc| doc.location())
                .and_then(|loc| loc.href().ok())
                .and_then(|href| Some(if href.ends_with('/') {
                    href[0..href.len() - 1].to_string()
                } else {
                    href
                }))
                .unwrap_or_else(|| {
                    log::error!("Unable to get baseurl from browser..trying fallback");
                    FALLBACK_URL.to_owned()
                })
        );

        reqwest_wasm::Url::parse(&url).map_err(|e| ErrorMessage {
            description: "Unable to parse URL".to_owned(),
            details: Some(e.to_string()),
        })
    }
}

#[cfg(test)]
mod test {
    use super::models::{ChatMessage, ChatType};

    const timestamp: &str = "2024-07-14T10:18:02.8379913+02:00";

    #[test]
    fn test_timestamp() {
        let message = ChatMessage {
            timestamp: timestamp.to_owned(),
            chat_type: ChatType::Say,
            sender_name: "none".to_owned(),
            text: "test".to_owned(),
        };

        let formatted = message.formatted_timestamp();
        println!("{formatted}");
        assert_eq!(formatted, "2024-07-14 10:18")
    }
}
