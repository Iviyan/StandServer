export class RequestError extends Error {
    constructor(response, json) {
      super(json?.title);
      this.name = 'RequestError';

	  this.rfc7807 = !!json;
      this.response = json;
      this.title = json?.title;
      this.status = response.status;
    }
  }
