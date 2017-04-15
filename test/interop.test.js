'use strict';

const assert = require('chai').assert;
const handler = require('./out/test/stubHandler').handler;

const execute = (request, context) =>
  new Promise((resolve, reject) => {
    handler(request, context, (err, data) => {
      if (err) {
        reject(err);
      } else {
        resolve(data);
      }
    })
  });

const session = {
  new: true,
  sessionId: 'string',
  application: {
    applicationId: 'string'
  },
  attributes: {
    string: {}
  },
  user: {
    userId: 'string',
      permissions: {
        consentToken: 'string'
    },
    accessToken: 'string'
  }
};

describe('Handler Interop', () => {
  it('executes a simple request ', () =>
    execute({
      version: '1.0',
      session: session,
      request: {
        type: 'LaunchRequest',
        requestId: 'string',
        timestamp: 'string',
        locale: 'string'
      },
    }, {
    }).then(response => {
      assert.deepEqual(response, {
        'version': '1.0.0',
        'sessionAttributes': {
          'AlexaFs': null
        },
        'response': {
          'shouldEndSession': false,
          'outputSpeech': {
            'type': 'PlainText',
            'text': 'Hello world!',
            'ssml': null
          },
          'reprompt': null,
          'card': null
        }
      });
    }));
});
