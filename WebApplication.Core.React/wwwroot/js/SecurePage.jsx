﻿class SecurePage extends React.Component {
   constructor(props) {
      super(props);
      let hasAccessToken = window.sessionStorage.getItem("access_token") != null;
      this.state = { loginError: null, authenticated: hasAccessToken };
   }
   signIn(username, password) {
      var form = new FormData();
      form.append('username', username);
      form.append('password', password);
      fetch('http://localhost:52000/token', { method: 'post', body: form })
         .then(response => {
            if (!response.ok) throw Error(response.statusText);
            return response.json();
         })
         .then(token => {
            window.sessionStorage.setItem("access_token", token.access_token);
            this.setState({ loginError: null, authenticated: true });
         })
         .catch(error => this.setState({ loginError: "Invalid user name or password" }));
   }
   signOut() {
      window.sessionStorage.clear();
      this.setState({ authenticated: false });
   }
   render() {
      let handleSubmit = info => this.signIn(info.username, info.password);
      let handleSignOut = () => this.signOut();
      return (
         <MuiThemeProvider>
            <div className="container-fluid">
               <div className="header clearfix">
                  <h3>Example: Secure Page *** UNDER CONSTRUCTION ***</h3>
               </div>
               {this.state.authenticated ?
                  <SecurePageView onSignOut={handleSignOut} /> :
                  <LoginForm onSubmit={handleSubmit} errorText={this.state.loginError} />}
            </div>
         </MuiThemeProvider>
      )
   }
}

class LoginForm extends React.Component {
   constructor(props) {
      super(props);
      this.state = { username: "demo", password: "dotnetify" };
   }
   render() {
      let handleUserNameChange = event => this.setState({ username: event.target.value });
      let handlePasswordChange = event => this.setState({ password: event.target.value });
      let handleSubmit = () => this.props.onSubmit(this.state);
      return (
         <div className="jumbotron">
            <h3>Sign in</h3>
            <p>
               <TextField id="UserName" floatingLabelText="User name" value={this.state.username} onChange={handleUserNameChange} errorText={this.props.errorText} /><br />
               <TextField id="Password" floatingLabelText="Password" value={this.state.password} onChange={handlePasswordChange} errorText={this.props.errorText} />
            </p>
            <RaisedButton label="Submit" primary={true} onClick={handleSubmit} />
         </div>
      );
   }
}

class SecurePageView extends React.Component {
   constructor(props) {
      super(props);
      this.state = {};

      let bearerToken = "Bearer " + window.sessionStorage.getItem("access_token");
      this.vm = dotnetify.react.connect("SecurePageVM", this, { headers: { Authorization: bearerToken } });
   }
   componentWillUnmount() {
      this.vm.$destroy();
   }
   render() {
      let handleSignOut = () => this.props.onSignOut();
      return (
         <div className="jumbotron">
            <h3>Secure View</h3>
            <RaisedButton label="Sign out" primary={true} onClick={handleSignOut} />
         </div>
      );
   }
}

