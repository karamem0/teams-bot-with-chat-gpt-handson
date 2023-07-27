# はじめての ChatGPT で Teams ボット ハンズオン

## 事前準備

**重要: 以下の事前準備を満たさない場合はハンズオンを実施できません。必ず確認をお願いします。**

- Microsoft 365 開発者プログラムで作成したテナント
    - Microsoft 365 開発者プログラムに参加し開発者向けサブスクリプションを有効にしたテナントを作成してください。  
      https://developer.microsoft.com/en-us/microsoft-365/dev-program/
- Microsoft Azure サブスクリプション
    - お持ちでない場合は無料試用版を利用できます。これにより $200 のクレジットが 30 日間無料で使用できます。無料試用版の開始にはクレジット カードが必要です。  
      https://azure.microsoft.com/ja-jp/pricing/offers/ms-azr-0044p/
    - Microsoft 365 開発者プログラムのテナントと同じである必要はありません。
- OpenAI アカウント
    - OpenAI にサインインてください。OpenAI API は有料ですが一定期間は無料枠が使用できます。  
      https://openai.com/

## 開発環境

- Windows または MacOS
- Visual Studio Code
  - C# 拡張機能
  - Azure App Service 拡張機能
- .NET Core SDK 6.0 (**重要: .NET Core SDK 7.0 がインストールされている環境では動作しません。**)
- Bot Framework Emulator

## ハンズオン

### .NET Core のバージョンの確認

この手順では、.NET Core SDK 6.0 がインストールされているかどうかを確認します。

1. インストールされている .NET Core SDK のバーションを表示します。

    ```
    dotnet --list-sdks
    ```

1. 表示された最新のバージョンを確認してください。
    - `6.0.*` の場合は以降の手順は必要ありません。
    - `7.0.*` の場合は、.NET Core のバージョンを指定するために、作業ディレクトリ (任意) に `global.json` ファイルを作成します。`{{sdk-version}}` の値は上記で表示された .NET Core 6.0 のバージョンを指定してください。

        ```json
        {
          "sdk": {
            "version": "{{sdk-version}}"
          }
        }
        ```

    - 上記以外の場合は .NET Core SDK 6.0 をインストールしてください。

### プロジェクトを作成する

この手順では、Bot Framework の EchoBot テンプレートを使用して、プロジェクトを作成するところまでを実施します。上記の手順で `global.json` を作成している場合は、ファイルのあるディレクトリで実施してください。

1. Bot Framework テンプレートをインストールします。

    ```
    dotnet new -i Microsoft.Bot.Framework.CSharp.EchoBot
    ```

1. プロジェクトを作成します。

    ```
    dotnet new echobot -n TeamsAIBot
    ```

1. `TeamsAIBot` を Visual Studio Code で開きます。

    ```
    cd TeamsAIBot
    code .
    ```

1. `tasks.json` を作成します。メニューの **ターミナル** - **タスクの構成** - **テンプレートから tasks.json を生成** - **.NET Core** をクリックします。
1. `launch.json` を作成します。メニューの **実行** - **構成の追加** - **.NET 5+ and .NET Core** をクリックします。
1. プロジェクトをビルドします。メニューの **ターミナル** - **ビルド タスクの実行** をクリックします。

### コードを修正する

この手順では、作成したプロジェクトに対して、Open AI と会話するためのコードを追加します。

1. OpenAI クライアント ライブラリのパッケージを追加します。

    ```
    dotnet add package Azure.AI.OpenAI --prerelease
    ```

1. Visual Code で `Startup.cs` を開き、以下を修正します。
    - `ConfigureService` メソッドに以下のコードを追加します。

        ```csharp
        services.AddSingleton<IStorage>(new MemoryStorage());
        services.AddSingleton<ConversationState>();
        ```

1. Visual Code で `Bots/EchoBot.cs` を開き、以下を修正します。
    - `using` のセクションに OpenAI クライアント ライブラリの宣言を追加します。

        ```csharp
        using Microsoft.Extensions.Configuration;
        using System;
        using Azure.AI.OpenAI;
        ```

    - コンストラクタとして以下のコードを追加します。`{{api-key}}` の部分を OpenAI から取得した API キーに変更します。

        ```csharp
        private readonly OpenAIClient chatClient;
        private readonly ConversationState conversationState;

        public EchoBot(IConfiguration configuration, ConversationState conversationState)
        {
            this.chatClient = new OpenAIClient("{{api-key}}");
            this.conversationState = conversationState;
        }
        ```

    - `OnMessageActivityAsync` メソッドを以下のコードで置き換えます。

        ```csharp
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var accessor = this.conversationState.CreateProperty<List<Tuple<string, string>>>(nameof(ChatMessage));
            var messages = await accessor.GetAsync(turnContext, () => new(), cancellationToken);
            while (messages.Count > 8)
            {
                messages.RemoveAt(0);
            }
            var chatCompletionsOptions = new ChatCompletionsOptions();
            chatCompletionsOptions.Messages.Add(new ChatMessage(
                ChatRole.System,
                "あなたは Microsoft Bot Framework から呼び出されるアシスタントです。ユーザーからの質問に回答してください。"
            ));
            foreach (var message in messages)
            {
                chatCompletionsOptions.Messages.Add(new ChatMessage(
                    new ChatRole(message.Item1),
                    message.Item2
                ));
            }
            chatCompletionsOptions.Messages.Add(new ChatMessage(ChatRole.User, turnContext.Activity.Text));
            var chatCompletion = await this.chatClient.GetChatCompletionsAsync(
                "gpt-3.5-turbo",
                chatCompletionsOptions,
                cancellationToken
            );
            var replyText = chatCompletion.Value.Choices[0].Message.Content;
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
            messages.Add(new Tuple<string, string>(ChatRole.User.ToString(), turnContext.Activity.Text));
            messages.Add(new Tuple<string, string>(ChatRole.Assistant.ToString(), replyText));
            await accessor.SetAsync(turnContext, messages, cancellationToken);
            await this.conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
       }
        ```

1. デバッグを開始します。**実行** - **デバッグの開始** をクリックします。以下のような画面が表示されることを確認します。

    ![001.png](./img/001.png)

1. Bot Framework Emulator から作成したボットに接続します。Bot Framework Emulator を起動し、**Open Bot** をクリックします。必要な項目を入力し **Connect** をクリックします。

    |項目名|項目値|
    |-|-|
    |Bot URL|`http://localhost:3978/api/messages`|

    ![002.png](./img/002.png)

1. チャット欄に質問を入力し回答が得られることを確認します。

    ![003.png](./img/003.png)

### Azure AD アプリケーションを作成する

この手順では、Azure Bot Service を動作させるための Azure AD アプリケーションを作成します。

1. [Microsoft Entra 管理センター](https://entra.microsoft.com) にアクセスし **Microsoft 365 開発者プログラムで作成したテナント** のアカウントでサインインします。
1. メニューの **ID** - **アプリケーション** - **アプリの登録** をクリックします。
1. **新規登録** をクリックします。
1. 以下の項目を入力し **登録** をクリックします。

    |項目名|項目値|
    |-|-|
    |名前|Teams AI Bot|
    |サポートされているアカウントの種類|この組織ディレクトリのみに含まれるアカウント (シングル テナント)|

    ![004.png](./img/004.png)

1. **概要** の **テナント ID** および **クライアント ID** の値をメモします。

    ![005.png](./img/005.png)

1. **証明書とシークレット** - **新しいクライアント シークレット** をクリックします。

1. **追加** をクリックします。
1. 作成された **クライアント シークレット** をメモします。

    ![006.png](./img/006.png)

1. メモした **テナント ID**、**クライアント ID**、**クライアント シークレット** を `appsettings.json` に貼り付けます。

    |項目名|項目値|
    |-|-|
    |MicrosoftAppType|`SingleTenant`|
    |MicrosoftAppId|**クライアント ID**|
    |MicrosoftAppPassword|**クライアント シークレット**|
    |MicrosoftAppTenantId|**テナント ID**|

### Azure Web Apps を作成する

この手順では、ボットが実際に動作する Web アプリを作成します。

1. [Microsoft Azure ポータル](https://portal.azure.com) にアクセスし **Microsoft Azure サブスクリプション** があるテナントのアカウントでサインインします。
1. メニューの **リソースの作成** - **Web** - **Web アプリ** をクリックします。
1. **基本** タブで以下の項目を入力し **次: デプロイ** をクリックします。

    |項目名|項目値|
    |-|-|
    |サブスクリプション|お使いのサブスクリプション|
    |リソース グループ|任意、ない場合は新規作成|
    |名前|任意、グローバルで一意になる名前|
    |公開|`コード`|
    |ランタイム スタック|`.NET 6 (LTS)`|
    |オペレーティング システム|`Windows`|
    |地域|任意、特になければ `Japan East`|
    |Windows プラン|任意、ない場合は新規作成|
    |価格プラン|`Basic B1`|

1. **デプロイ** タブで **次: ネットワーク** をクリックします。
1. **ネットワーク** タブで **次: 監視** をクリックします。
1. **監視** タブで以下の項目を入力し **次: タグ** をクリックします。

    |項目名|項目値|
    |-|-|
    |Application Insights を有効にする|`いいえ`|

1. **タグ** タブで **次: 確認および作成** をクリックします。
1. **確認および作成** タブで **作成** をクリックします。

### Azure Web Apps にコードをデプロイする

この手順では、作成した Azure Web Apps にコードをデプロイします。

1. Visual Studio Code の **Azure** タブで **Azure にサインイン** をクリックします。
1. **Microsoft Azure サブスクリプション** があるテナントのアカウントでサインインします。
1. **お使いのサブスクリプション** - **App Services** - **作成した Azure Web Apps** を右クリックし、**Deploy to Web App** をクリックします。
1. **TeamsAIBot** を選択します。
1. **Add Config** をクリックします。
1. **Deploy** をクリックします。
1. デプロイが完了したら **Browse Website** をクリックしてサイトが表示されることを確認します。

### Azure Bot Service を作成する

この手順では、ボットを Teams に公開するための Azure Bot Service を作成します。

1. [Microsoft Azure ポータル](https://portal.azure.com) にアクセスし **Microsoft Azure サブスクリプション** があるテナントのアカウントでサインインします。
1. メニューの **リソースの作成** - **Web** - **Azure Bot** をクリックします。
1. **基本** タブで以下の項目を入力し **次へ** をクリックします。

    |項目名|項目値|
    |-|-|
    |ボット ハンドル|任意、グローバルで一意になる名前|
    |サブスクリプション|お使いのサブスクリプション|
    |リソース グループ|Azure Web Apps を作成したのとと同じリソース グループ|
    |データ所在地|`グローバル`|
    |価格レベル|`Free`|
    |アプリの種類|`シングル テナント`|
    |作成の種類|既存のアプリの登録を使用する|
    |アプリ ID|上記の手順で作成した **クライアント ID**|
    |アプリ テナント ID|上記の手順で作成した **テナント ID**|

1. **タグ** タブで **次: 確認および作成** をクリックします。
1. **確認および作成** タブで **作成** をクリックします。

### Azure Bot Service を構成する

この手順では、Azure Bot Service をボットに接続するための構成を設定します。

1. 上記の手順で作成した Azure Bot Service を開きます。
1. **構成** タブで以下の項目を入力し **適用** をクリックします。`{{web-app-name}}` の部分は上記の手順で作成した Azure Web Apps の名前で置き換えます。

    |項目名|項目値|
    |-|-|
    |メッセージ エンドポイント|`https://{{web-app-name}}.azurewebsites.net/api/messages`|

    ![007.png](./img/007.png)

1. **チャンネル** タブで **Microsoft Teams** をクリックします。
1. サービス条件のダイアログで **同意** をクリックします。
1. **メッセージング** タブで **適用** をクリックします。

    ![008.png](./img/008.png)

1. **Web チャットでテスト** をクリックします。チャット欄に質問を入力し回答が得られることを確認します。

    ![009.png](./img/009.png)

### Teams アプリを作成する

1. [Microsoft Teams 開発者ポータル](https://dev.teams.microsoft.com) にアクセスし **Microsoft 365 開発者プログラムで作成したテナント** のアカウントでサインインします。
1. **Apps** - **New app** をクリックします。

    ![010.png](./img/010.png)

1. アプリ名を入力して **Add** をクリックします。 

    ![011.png](./img/011.png)

1. **Basic information** をクリックします。以下の項目を入力し **Save** をクリックします。`{{web-app-name}}` の部分は上記の手順で作成した Azure Web Apps の名前で置き換えます。

    |項目名|項目値|
    |-|-|
    |Short description|Teams AI Bot|
    |Long description|Teams AI Bot|
    |Developer or company name|TeamsAIBot|
    |Website|`https://{{web-app-name}}.azurewebsites.net/`|
    |Privacy policy|`https://{{web-app-name}}.azurewebsites.net/`|
    |Terms of use|`https://{{web-app-name}}.azurewebsites.net/`|
    |Application (client) ID|上記の手順で作成した **クライアント ID**|

    ![012.png](./img/012.png)

1. **App feature** - **Bot** をクリックします。

    ![013.png](./img/013.png)

1. **Bot** ページで以下の項目を入力し **Save** をクリックします。

    |項目名|項目値|
    |-|-|
    |Identify your bot|`Enter a bot ID`|
    ||上記の手順で作成した **クライアント ID**|
    |What can your bot do?|未選択|
    |Select the scopes in which people can use this command|`Personal`|

    ![014.png](./img/014.png)

### Teams アプリを実行する

1. **Preview in Teams** をクリックします。
1. Teams をどのアプリで開くかを確認されるので **代わりに Web アプリを使用** をクリックします。

    ![015.png](./img/015.png)

1. アプリの情報が表示されるので **追加** をクリックします。

    ![016.png](./img/016.png)

1. チャット欄に質問を入力し回答が得られることを確認します。

    ![017.png](./img/017.png)
