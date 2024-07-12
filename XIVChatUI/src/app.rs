use std::sync::atomic::{AtomicBool, Ordering};

const REFRESH_TIME_SEC: u64 = 3;

use models::{ChatMessage, NewMessageRequest};
use requests::{get_messages, send_message};
use wasm_bindgen::JsCast;
use web_sys::{FormData, HtmlFormElement};
use yew::prelude::*;

pub enum FetchState<T, E> {
    Success(T),
    Error(E),
}

pub enum Msg {
    SetFetchState(FetchState<Vec<ChatMessage>, String>),
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
    messages: FetchState<Vec<ChatMessage>, String>,
    fetch_queued: AtomicBool,
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
                                    form.reset();
                                    data
                                } else {
                                    Err("unable to get form reference".to_owned())
                                };
                                cb.emit(Msg::SubmitMessage(res));
                            }
                        }}>
                        <label for="chatType">{ "ChatType:" }</label>
                        <select name="chatType" id="chatType">
                            <option value="Say">{ "Say" }</option>
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
            messages: FetchState::Success(Vec::new()),
            fetch_queued: AtomicBool::new(true),
        }
    }

    fn update(&mut self, ctx: &Context<Self>, msg: Self::Message) -> bool {
        match msg {
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
                self.messages = state;
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
                ctx.link().send_future(async {
                    match get_messages().await {
                        Ok(messages) => Msg::SetFetchState(FetchState::Success(messages)),
                        Err(e) => Msg::SetFetchState(FetchState::Error(e)),
                    }
                });
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
                                Err(e) => Msg::SetFetchState(FetchState::Error(e)),
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
        <div class="chatBoxHeader">
            <div class="manualRefresh">
                <button onclick={ctx.link().callback(|_| Msg::GetMessages)} style="width: fit-content; align-self: center;">
                            { "Refresh" }
                            </button>
            </div>
            <div class="refreshSettings">
                <input type="checkbox" id="refresh" name="refresh" checked={self.state.refresh_enabled} onclick={ctx.link().callback(|_| Msg::ToggleRefresh) }/>
                <label for="refresh">{ "Auto Refresh" }</label>
            </div>
        </div>
        <div class="chatBoxContent">
            {
                match &self.messages {
                    FetchState::Success(messages) => {
                        html! {
                            <>
                            {
                                messages.iter()
                                    .rev()
                                    .map(|msg| {
                                        let color = msg.chat_type.get_color();
                                        let uniq = format!("{}_{}", msg.timestamp, msg.sender_name);
                                        html!{
                                            <>
                                                <div key={uniq} class="chatEntry">
                                                    <div class="timestamp"> { format!("[{}]", msg.formatted_timestamp()) } </div>
                                                    <div class="chatType" style= { format!("color: {color}") }> { format!("[{}]", msg.chat_type) } </div>
                                                    if !msg.sender_name.is_empty() { <div class="sender" style= { format!("color: {color}") }> { format!("{}:", msg.sender_name) } </div> }
                                                    <div class="chatMessage" style= { format!("color: {color}") }>{ format!("{text}", text = msg.text) } </div>
                                                </div>
                                            </>
                                        }
                                    })
                                    .collect::<Html>()
                            }
                            </>
                        }
                    }
                    FetchState::Error(e) => html! {
                        <>
                            <p>{ format!("{e}") }</p>
                        </>
                    },
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
        Tell,
        Party,
        FreeCompany,
        Alliance,
        CrossParty,
        LinkShell1,
        LinkShell2,
        LinkShell3,
        LinkShell4,
        LinkShell5,
        LinkShell6,
        LinkShell7,
        LinkShell8,
        CrossLinkShell1,
        CrossLinkShell2,
        CrossLinkShell3,
        CrossLinkShell4,
        CrossLinkShell5,
        CrossLinkShell6,
        CrossLinkShell7,
        CrossLinkShell8,
        Novice,
        CustomEmotes,
        StandardEmotes,
        Echo,
        SystemError,
        SystemMessage,
        ErrorMessage,
        GatheringSystemMessage,
        NPCDialogueAnnouncements,
        RetainerSale,
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
                ChatType::Tell => "Tell",
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
                ChatType::Novice => "NN",
                ChatType::CustomEmotes => "CEmote",
                ChatType::StandardEmotes => "Emote",
                ChatType::Echo => "Echo",
                ChatType::SystemError => "Error(System)",
                ChatType::SystemMessage => "System",
                ChatType::ErrorMessage => "Error",
                ChatType::GatheringSystemMessage => "Gathering",
                ChatType::NPCDialogueAnnouncements => "NPC",
                ChatType::RetainerSale => "Retainer",
            };
            write!(f, "{str}")
        }
    }

    impl ChatType {
        pub fn get_color(&self) -> String {
            match self {
                Self::FreeCompany => "#4ef542",
                Self::Party => "#426ff5",
                cwl if (Self::CrossLinkShell1..=Self::CrossLinkShell8).contains(cwl) => "#9f3cbd",
                _ => "#FFFFFFFF",
            }
            .to_owned()
        }
    }

    impl ChatMessage {
        pub fn formatted_timestamp(&self) -> String {
            if let Ok(date_time) = self.timestamp.parse::<chrono::DateTime<chrono::Utc>>() {
                date_time.format("%Y-%m-%d %H:%M").to_string()
            } else {
                "N/A".to_owned()
            }
        }
    }
}

pub mod requests {
    use serde::de;

    use super::models::{ChatMessage, NewMessageRequest};

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

    pub async fn get_messages() -> Result<Vec<ChatMessage>, String> {
        let response = CLIENT
            .clone()
            .get(url(MESSAGES_URI)?)
            .send()
            .await
            .map_err(|e| format!("get failed: {e}"))?;
        from_response::<Vec<ChatMessage>>(response).await
    }

    pub async fn send_message(msg: &NewMessageRequest) -> Result<(), String> {
        let response = CLIENT
            .clone()
            .post(url(MESSAGES_URI)?)
            .json(msg)
            .send()
            .await
            .map_err(|e| format!("post failed: {e}"))?;

        match response.status().as_u16() {
            200..=299 => Ok(()),
            400 => Err(format!(
                "Bad request: {}",
                response.text().await.unwrap_or_else(|_| String::new())
            )),
            unknown_code => Err(format!("unexpected response: {}", unknown_code)),
        }
    }

    async fn from_response<T>(value: reqwest_wasm::Response) -> Result<T, String>
    where
        T: de::DeserializeOwned,
    {
        match value.status().as_u16() {
            200..=299 => {}
            _ => {
                return Err(format!(
                    "HTTP Error => {error_code}: {error_msg}",
                    error_code = value.status(),
                    error_msg = value.text().await.unwrap_or_else(|_| String::new()),
                ))
            }
        }

        let bytes = value
            .text()
            .await
            .map_err(|e| format!("Unable to read response: {e}"))?;

        serde_json::from_str(&bytes).map_err(|e| format!("JSON parsing failed: {e}"))
    }

    fn url(uri: &str) -> Result<reqwest_wasm::Url, String> {
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
        // let url = format!("{FALLBACK_URL}{uri}");

        reqwest_wasm::Url::parse(&url).map_err(|e| format!("Unable to parse URL: {e}"))
    }
}
