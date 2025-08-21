const rules = require('./webpack.rules');
const webpack = require('webpack');

rules.push({
  test: /\.css$/,
  use: [{ loader: 'style-loader' }, { loader: 'css-loader' }],
});

module.exports = {
  // Put your normal webpack config below here
  module: {
    rules,
  },
  devServer: {
    hot: true,
    port: 3000,
    headers: {
      'Access-Control-Allow-Origin': '*'
    }
  },
  plugins: [
    new webpack.HotModuleReplacementPlugin()
  ]
};
