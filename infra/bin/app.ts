#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { LoveBehaviorTranslatorStack } from '../lib/love-behavior-translator-stack';

const app = new cdk.App();

new LoveBehaviorTranslatorStack(app, 'LoveBehaviorTranslatorStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: process.env.CDK_DEFAULT_REGION,
  },
});


