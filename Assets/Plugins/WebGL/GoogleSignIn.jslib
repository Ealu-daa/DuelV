mergeInto(LibraryManager.library, {

  // Google Identity Servicesのスクリプトを読み込んで初期化する。
  // ページ内で一度だけ呼べばよい(FirebaseAuthBridge.Start()から呼ばれる)。
  InitGoogleSignIn: function (clientIdPtr) {
    var clientId = UTF8ToString(clientIdPtr);

    if (window.__duelvGsiLoading) return;
    window.__duelvGsiLoading = true;

    var script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.onload = function () {
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: function (response) {
          // response.credential = GoogleのIDトークン(JWT)。
          // Unity側のFirebaseAuthBridgeというGameObject名で待ち受けているので、
          // シーン上のGameObject名は必ず"FirebaseAuthBridge"にしておくこと(Autoで生成された物だとNG)。
          SendMessage('FirebaseAuthBridge', 'OnGoogleCredentialReceived', response.credential);
        }
      });
      window.__duelvGsiReady = true;
    };
    document.head.appendChild(script);
  },

  // Googleサインインのプロンプトを表示する(ボタンクリックから呼ぶ)。
  PromptGoogleSignIn: function () {
    if (window.__duelvGsiReady && window.google && window.google.accounts && window.google.accounts.id) {
      window.google.accounts.id.prompt();
    } else {
      console.error('[GoogleSignIn] Google Identity Servicesがまだ読み込み中です。少し待ってから再度お試しください。');
    }
  }

});
